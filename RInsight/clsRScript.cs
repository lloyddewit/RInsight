using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace RInsight;


// TODO Should we model constants differently to syntactic names? (there are five types of constants: integer, logical, numeric, complex and string)
// TODO Test special constants {"NULL", "NA", "Inf", "NaN"}
// TODO Test function names as string constants. E.g 'x + y can equivalently be written "+"(x, y). Notice that since '+' is a non-standard function name, it needs to be quoted (see https://cran.r-project.org/doc/manuals/r-release/R-lang.html#Writing-functions)'
// TODO handle '...' (used in function definition)
// TODO handle '.' normally just part of a syntactic name, but has a special meaning when in a function name, or when referring to data (represents no variable)
// TODO is it possible for packages to be nested (e.g. 'p1::p1_1::f()')?
// TODO currently all newlines (vbLf, vbCr and vbCrLf) are converted to vbLf. Is it important to remember what the original new line character was?
// TODO convert public data members to properties (all classes)
// TODO provide an option to get script with automatic indenting (specifiy num spaces for indent and max num Columns per line)
// 
// 17/11/20
// - allow named operator params (R-Instat allows operator params to be named, but this infor is lost in script)
// 
// 01/03/21
// - how should bracket operator separators be modelled?
// strInput = "df[1:2,]"
// strInput = "df[,1:2]"
// strInput = "df[1:2,1:2]"
// strInput = "df[1:2,""x""]"
// strInput = "df[1:2,c(""x"",""y"")]"
// 

    /// <summary>   TODO Add class summary. </summary>
public class clsRScript
{

    /// <summary>   
    /// The R statements in the script. The dictionary key is the start position of the statement 
    /// in the script. The dictionary value is the statement itself. </summary>
    public OrderedDictionary dctRStatements = new OrderedDictionary();

    /// <summary>   The current state of the token parsing. </summary>
    private enum typTokenState
    {
        WaitingForOpenCondition,
        WaitingForCloseCondition,
        WaitingForStartScript,
        WaitingForEndScript
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Parses the R script in <paramref name="strInput"/> and populates the distionary
    ///             of R statements.
    ///             <para>
    ///             This subroutine will accept, and correctly process all valid R. However, this 
    ///             class does not attempt to validate <paramref name="strInput"/>. If it is not 
    ///             valid R then this subroutine may still process the script without throwing an 
    ///             exception. In this case, the list of R statements will be undefined.
    ///             </para><para>
    ///             In other words, this subroutine should not generate false negatives (reject 
    ///             valid R) but may generate false positives (accept invalid R).
    ///             </para></summary>
    /// 
    /// <param name="strInput"> The R script to parse. This must be valid R according to the 
    ///                         R language specification at 
    ///                         https://cran.r-project.org/doc/manuals/r-release/R-lang.html 
    ///                         (referenced 01 Feb 2021).</param>
    /// --------------------------------------------------------------------------------------------
    public clsRScript(string strInput)
    {
        if (string.IsNullOrEmpty(strInput))
        {
            return;
        }

        var lstLexemes = GetLstLexemes(strInput);
        var lstTokens = GetLstTokens(lstLexemes);

        int iPos = 0;
        var dctAssignments = new Dictionary<string, clsRStatement>();
        while (iPos < lstTokens.Count)
        {
            uint iScriptPos = lstTokens[iPos].iScriptPos;
            var clsStatement = new clsRStatement(lstTokens, ref iPos, dctAssignments);
            dctRStatements.Add(iScriptPos, clsStatement);

            // if the value of an assigned element is new/updated
            if (!(clsStatement.clsAssignment == null))
            {
                // store the updated/new definition in the dictionary
                if (dctAssignments.ContainsKey(clsStatement.clsAssignment.strTxt))
                {
                    dctAssignments[clsStatement.clsAssignment.strTxt] = clsStatement;
                }
                else
                {
                    dctAssignments.Add(clsStatement.clsAssignment.strTxt, clsStatement);
                }
            }

        }
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns <paramref name="script"/> as a list of its constituent lexemes. 
    ///             A lexeme is a string of characters that represents a valid R element 
    ///             (identifier, operator, keyword, seperator, bracket etc.). A lexeme does not 
    ///             include any type information.
    ///             <para>
    ///             This function identifies lexemes using a technique known as 'longest match' 
    ///             or 'maximal munch'. It keeps adding characters to the lexeme one at a time 
    ///             until it reaches a character that is not in the set of characters acceptable 
    ///             for that lexeme.
    ///             </para></summary>
    /// 
    /// <param name="script"> The R script to convert (must be syntactically correct R). </param>
    /// 
    /// <returns>   <paramref name="script"/> as a list of its constituent lexemes. </returns>
    /// --------------------------------------------------------------------------------------------
    public static List<string> GetLstLexemes(string script)
    {
        var lexemes = new List<string>();
        if (script.Length == 0)
        {
            return lexemes;
        }
        
        string lexeme = "";
        var bracketStack = new Stack<bool>();

        foreach (char lexemeChar in script)
        {
            // we keep adding characters to the lexeme, one at a time, until we reach a character that 
            // would make the lexeme invalid.
            // Second part of condition is edge case for nested operator brackets (see note below).
            if (clsRToken.IsValidLexeme(lexeme + lexemeChar) && 
                !(lexeme + lexemeChar == "]]" && 
                  (bracketStack.Count < 1 || bracketStack.Peek()))) 
            {
                lexeme += lexemeChar;
                continue;
            }

            // Edge case: We need to handle nested operator brackets e.g. 'k[[l[[m[6]]]]]'. 
            // For the above example, we need to recognise that the ']' to the right 
            // of '6' is a single ']' bracket and is not part of a double ']]' bracket.
            // To achieve this, we push each open bracket to a stack so that we know 
            // which type of closing bracket is expected for each open bracket.
            switch (lexeme)
            {
                case "[":
                    {
                        bracketStack.Push(true);
                        break;
                    }
                case "[[":
                    {
                        bracketStack.Push(false);
                        break;
                    }
                case "]":
                case "]]":
                    {
                        if (bracketStack.Count < 1)
                        {
                            throw new Exception("Closing bracket detected ('" + lexeme + "') with no corresponding open bracket.");
                        }
                        bracketStack.Pop();
                        break;
                    }
            }

            // adding the new char to the lexeme would make the lexeme invalid, 
            // so we add the existing lexeme to the list and start a new lexeme
            lexemes.Add(lexeme);
            lexeme = lexemeChar.ToString();
        }

        lexemes.Add(lexeme);
        return lexemes;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns <paramref name="lstLexemes"/> as a list of tokens.
    ///             <para>
    ///             A token is a string of characters that represent a valid R element, plus meta 
    ///             data about the token type (identifier, operator, keyword, bracket etc.). 
    ///             </para></summary>
    /// 
    /// <param name="lstLexemes">   The list of lexemes to convert to tokens. </param>
    /// 
    /// <returns>   <paramref name="lstLexemes"/> as a list of tokens. </returns>
    /// --------------------------------------------------------------------------------------------
    public static List<clsRToken> GetLstTokens(List<string> lstLexemes)
    {

        if (lstLexemes is null || lstLexemes.Count == 0)
        {
            return null;
        }

        var lstRTokens = new List<clsRToken>();
        string strLexemePrev = "";
        string strLexemeCurrent = "";
        string strLexemeNext;
        bool bLexemePrevOnSameLine = false;
        bool bLexemeNextOnSameLine;
        bool bStatementContainsElement = false;
        clsRToken clsToken;

        var stkNumOpenBrackets = new Stack<int>();
        stkNumOpenBrackets.Push(0);

        var stkIsScriptEnclosedByCurlyBrackets = new Stack<bool>();
        stkIsScriptEnclosedByCurlyBrackets.Push(true);

        var stkTokenState = new Stack<typTokenState>();
        stkTokenState.Push(typTokenState.WaitingForStartScript);

        uint iScriptPos = 0U;

        for (int iPos = 0, loopTo = lstLexemes.Count - 1; iPos <= loopTo; iPos++)
        {

            if (stkNumOpenBrackets.Count < 1)
            {
                throw new Exception("The stack storing the number of open brackets must have at least one value.");
            }
            else if (stkIsScriptEnclosedByCurlyBrackets.Count < 1)
            {
                throw new Exception("The stack storing the number of open curly brackets must have at least one value.");
            }
            else if (stkTokenState.Count < 1)
            {
                throw new Exception("The stack storing the current state of the token parsing must have at least one value.");
            }

            // store previous non-space lexeme
            if (clsRToken.IsElement(strLexemeCurrent))
            {
                strLexemePrev = strLexemeCurrent;
                bLexemePrevOnSameLine = true;
            }
            else if (clsRToken.IsNewLine(strLexemeCurrent))
            {
                bLexemePrevOnSameLine = false;
            }

            strLexemeCurrent = lstLexemes[iPos];
            bStatementContainsElement = bStatementContainsElement ? bStatementContainsElement : clsRToken.IsElement(strLexemeCurrent);

            // find next lexeme that represents an R element
            strLexemeNext = null;
            bLexemeNextOnSameLine = true;
            for (int iNextPos = iPos + 1, loopTo1 = lstLexemes.Count - 1; iNextPos <= loopTo1; iNextPos++)
            {
                if (clsRToken.IsElement(lstLexemes[iNextPos]))
                {
                    strLexemeNext = lstLexemes[iNextPos];
                    break;
                }
                switch (lstLexemes[iNextPos] ?? "")
                {
                    case Constants.vbLf:
                    case Constants.vbCr:
                    case var @case when @case == Constants.vbCr:
                        {
                            bLexemeNextOnSameLine = false;
                            break;
                        }
                }
            }

            // determine whether the current sequence of tokens makes a complete valid R statement
            // This is needed to determine whether a newline marks the end of the statement
            // or is just for presentation.
            // The current sequence of tokens is considered a complete valid R statement if it 
            // has no open brackets and it does not end in an operator.
            switch (strLexemeCurrent ?? "")
            {
                case "(":
                case "[":
                case "[[":
                    {
                        stkNumOpenBrackets.Push(stkNumOpenBrackets.Pop() + 1);
                        break;
                    }
                case ")":
                case "]":
                case "]]":
                    {
                        stkNumOpenBrackets.Push(stkNumOpenBrackets.Pop() - 1);
                        break;
                    }
                case "if":
                case "while":
                case "for":
                case "function":
                    {
                        stkTokenState.Push(typTokenState.WaitingForOpenCondition);
                        stkNumOpenBrackets.Push(0);
                        break;
                    }
                case "else":
                case "repeat":
                    {
                        stkTokenState.Push(typTokenState.WaitingForCloseCondition); // 'else' and 'repeat' keywords have no condition (e.g. 'if (x==1) y<-0 else y<-1'
                        stkNumOpenBrackets.Push(0);                                 // after the keyword is processed, the state will automatically change to 'WaitingForEndScript'
                        break;
                    }
            }

            // identify the token associated with the current lexeme and add the token to the list
            clsToken = new clsRToken(strLexemePrev, strLexemeCurrent, strLexemeNext, bLexemePrevOnSameLine, bLexemeNextOnSameLine, iScriptPos);
            iScriptPos = (uint)(iScriptPos + strLexemeCurrent.Length);

            // Process key words
            // Determine whether the next end statement will also be the end of the current script.
            // Normally, a '}' indicates the end of the current script. However, R allows single
            // statement scripts, not enclosed with '{}' for selected key words. 
            // The key words that allow this are: if, else, while, for and function.
            // For example:
            // if(x <= 0) y <- log(1+x) else y <- log(x)
            if (clsToken.enuToken == clsRToken.typToken.RComment || clsToken.enuToken == clsRToken.typToken.RSpace)       // ignore comments, spaces and newlines (they don't affect key word processing)
            {
            }
            // clsToken.enuToken = clsRToken.typToken.RNewLine Then
            // clsToken.enuToken = clsRToken.typToken.RKeyWord Then    'ignore keywords (already processed above)
            // do nothing
            else
            {
                switch (stkTokenState.Peek())
                {
                    case typTokenState.WaitingForOpenCondition:
                        {

                            if (!(clsToken.enuToken == clsRToken.typToken.RNewLine))
                            {
                                if (clsToken.strTxt == "(")
                                {
                                    stkTokenState.Pop();
                                    stkTokenState.Push(typTokenState.WaitingForCloseCondition);
                                }
                            }

                            break;
                        }

                    case typTokenState.WaitingForCloseCondition:
                        {

                            if (stkNumOpenBrackets.Peek() == 0)
                            {
                                stkTokenState.Pop();
                                stkTokenState.Push(typTokenState.WaitingForStartScript);
                            }

                            break;
                        }

                    case typTokenState.WaitingForStartScript:
                        {

                            if (!(clsToken.enuToken == clsRToken.typToken.RComment || clsToken.enuToken == clsRToken.typToken.RPresentation || clsToken.enuToken == clsRToken.typToken.RSpace || clsToken.enuToken == clsRToken.typToken.RNewLine))
                            {
                                stkTokenState.Pop();
                                stkTokenState.Push(typTokenState.WaitingForEndScript);
                                if (clsToken.strTxt == "{")
                                {
                                    stkIsScriptEnclosedByCurlyBrackets.Push(true);  // script will terminate with '}'
                                }
                                else
                                {
                                    stkIsScriptEnclosedByCurlyBrackets.Push(false);
                                } // script will terminate with end statement
                            }

                            break;
                        }

                    case typTokenState.WaitingForEndScript:
                        {

                            if (clsToken.enuToken == clsRToken.typToken.RNewLine && bStatementContainsElement && stkNumOpenBrackets.Peek() == 0 && !clsRToken.IsOperatorUserDefined(strLexemePrev) && !(clsRToken.IsOperatorReserved(strLexemePrev) && !(strLexemePrev == "~")))                   // if statement contains at least one R element (i.e. not just spaces, comments, or newlines)
                                                                                                                                                                                                                                                                                                   // if there are no open brackets
                                                                                                                                                                                                                                                                                                   // if line doesn't end in a user-defined operator
                                                                                                                                                                                                                                                                                                   // if line doesn't end in a predefined operator
                                                                                                                                                                                                                                                                                                   // unless it's a tilda (the only operator that doesn't need a right-hand value)
                            {
                                clsToken.enuToken = clsRToken.typToken.REndStatement;
                                bStatementContainsElement = false;
                            }

                            if (clsToken.enuToken == clsRToken.typToken.REndStatement && stkIsScriptEnclosedByCurlyBrackets.Peek() == false && string.IsNullOrEmpty(strLexemeNext))
                            {
                                clsToken.enuToken = clsRToken.typToken.REndScript;
                            }

                            if (clsToken.enuToken == clsRToken.typToken.REndScript)
                            {
                                stkIsScriptEnclosedByCurlyBrackets.Pop();
                                stkNumOpenBrackets.Pop();
                                stkTokenState.Pop();
                            }

                            break;
                        }

                    default:
                        {
                            throw new Exception("The token is in an unknown state.");
                        }
                }
            }

            // add new token to token list
            lstRTokens.Add(clsToken);

            // Edge case: if the script has ended and there are no more R elements to process, 
            // then ensure that only formatting lexemes (i.e. spaces, newlines or comments) follow
            // the script's final statement.
            if (clsToken.enuToken == clsRToken.typToken.REndScript && string.IsNullOrEmpty(strLexemeNext))
            {

                for (int iNextPos = iPos + 1, loopTo2 = lstLexemes.Count - 1; iNextPos <= loopTo2; iNextPos++)
                {

                    strLexemeCurrent = lstLexemes[iNextPos];

                    clsToken = new clsRToken("", strLexemeCurrent, "", false, false, iScriptPos);
                    iScriptPos = (uint)(iScriptPos + strLexemeCurrent.Length);

                    switch (clsToken.enuToken)
                    {
                        case clsRToken.typToken.RSpace:
                        case clsRToken.typToken.RNewLine:
                        case clsRToken.typToken.RComment:
                            {
                                break;
                            }

                        default:
                            {
                                throw new Exception("Only spaces, newlines and comments are allowed after the script ends.");
                            }
                    }

                    // add new token to token list
                    lstRTokens.Add(clsToken);

                }
                return lstRTokens;
            }
        }

        return lstRTokens;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns this object as a valid, executable R script. </summary>
    /// 
    /// <param name="bIncludeFormatting">   If True, then include all formatting information in 
    ///     returned string (comments, indents, padding spaces, extra line breaks etc.). </param>
    /// 
    /// <returns>   The current state of this object as a valid, executable R script. </returns>
    /// --------------------------------------------------------------------------------------------
    public string GetAsExecutableScript(bool bIncludeFormatting = true)
    {
        string strTxt = "";
        foreach (DictionaryEntry entry in dctRStatements)
        {
            clsRStatement rStatement = (clsRStatement)entry.Value;
            strTxt = Conversions.ToString(strTxt + Operators.ConcatenateObject(rStatement.GetAsExecutableScript(bIncludeFormatting), bIncludeFormatting ? "" : Constants.vbLf));
        }
        return strTxt;
    }

}