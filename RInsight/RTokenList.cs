using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

namespace RInsight;

/// <summary>
/// TODO
/// </summary>
public class RTokenList {

    /// <summary>   The current state of the token parsing. </summary>
    private enum typTokenState
    {
        WaitingForOpenCondition,
        WaitingForCloseCondition,
        WaitingForStartScript,
        WaitingForEndScript
    }

    public List<RToken>? Tokens { get; }

    public RTokenList(string script)
    {
        Tokens = GetLstTokens(script);
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
    private static List<RToken>? GetLstTokens(string script) {

        List<string> lstLexemes = GetLstLexemes(script);

        if (lstLexemes is null || lstLexemes.Count == 0)
        {
            return null;
        }

        var lstRTokens = new List<RToken>();
        string strLexemePrev = "";
        string strLexemeCurrent = "";
        string strLexemeNext = "";
        bool bLexemePrevOnSameLine = false;
        bool bLexemeNextOnSameLine;
        bool bStatementContainsElement = false;
        RToken clsToken;

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
            if (RToken.IsElement(strLexemeCurrent))
            {
                strLexemePrev = strLexemeCurrent;
                bLexemePrevOnSameLine = true;
            }
            else if (RToken.IsNewLine(strLexemeCurrent))
            {
                bLexemePrevOnSameLine = false;
            }

            strLexemeCurrent = lstLexemes[iPos];
            bStatementContainsElement = bStatementContainsElement ? bStatementContainsElement : RToken.IsElement(strLexemeCurrent);

            // find next lexeme that represents an R element
            strLexemeNext = "";
            bLexemeNextOnSameLine = true;
            for (int iNextPos = iPos + 1, loopTo1 = lstLexemes.Count - 1; iNextPos <= loopTo1; iNextPos++)
            {
                if (RToken.IsElement(lstLexemes[iNextPos]))
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
            if (strLexemeCurrent == null)
            {
                throw new Exception("The current lexeme cannot be null.");
            }
            clsToken = new RToken(strLexemePrev, strLexemeCurrent, strLexemeNext, bLexemePrevOnSameLine, bLexemeNextOnSameLine, iScriptPos);
            iScriptPos = (uint)(iScriptPos + strLexemeCurrent.Length);

            // Process key words
            // Determine whether the next end statement will also be the end of the current script.
            // Normally, a '}' indicates the end of the current script. However, R allows single
            // statement scripts, not enclosed with '{}' for selected key words. 
            // The key words that allow this are: if, else, while, for and function.
            // For example:
            // if(x <= 0) y <- log(1+x) else y <- log(x)
            if (clsToken.tokentype == RToken.TokenType.RComment || clsToken.tokentype == RToken.TokenType.RSpace)
            {       // ignore comments, spaces and newlines (they don't affect key word processing)
                    // clsToken.enuToken = clsRToken.typToken.RNewLine Then
                    // clsToken.enuToken = clsRToken.typToken.RKeyWord Then    'ignore keywords (already processed above)
                    // do nothing
            }
            else
            {
                switch (stkTokenState.Peek())
                {
                    case typTokenState.WaitingForOpenCondition:
                        {

                            if (!(clsToken.tokentype == RToken.TokenType.RNewLine))
                            {
                                if (clsToken.text == "(")
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

                            if (!(clsToken.tokentype == RToken.TokenType.RComment || clsToken.tokentype == RToken.TokenType.RPresentation || clsToken.tokentype == RToken.TokenType.RSpace || clsToken.tokentype == RToken.TokenType.RNewLine))
                            {
                                stkTokenState.Pop();
                                stkTokenState.Push(typTokenState.WaitingForEndScript);
                                if (clsToken.text == "{")
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

                            if (clsToken.tokentype == RToken.TokenType.RNewLine && bStatementContainsElement && stkNumOpenBrackets.Peek() == 0 && !RToken.IsOperatorUserDefined(strLexemePrev) && !(RToken.IsOperatorReserved(strLexemePrev) && !(strLexemePrev == "~")))
                            {                  // if statement contains at least one R element (i.e. not just spaces, comments, or newlines)
                                               // if there are no open brackets
                                               // if line doesn't end in a user-defined operator
                                               // if line doesn't end in a predefined operator
                                               // unless it's a tilda (the only operator that doesn't need a right-hand value) {
                                clsToken.tokentype = RToken.TokenType.REndStatement;
                                bStatementContainsElement = false;
                            }

                            if (clsToken.tokentype == RToken.TokenType.REndStatement && stkIsScriptEnclosedByCurlyBrackets.Peek() == false && string.IsNullOrEmpty(strLexemeNext))
                            {
                                clsToken.tokentype = RToken.TokenType.REndScript;
                            }

                            if (clsToken.tokentype == RToken.TokenType.REndScript)
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
            if (clsToken.tokentype == RToken.TokenType.REndScript && string.IsNullOrEmpty(strLexemeNext))
            {

                for (int iNextPos = iPos + 1, loopTo2 = lstLexemes.Count - 1; iNextPos <= loopTo2; iNextPos++)
                {

                    strLexemeCurrent = lstLexemes[iNextPos];

                    clsToken = new RToken("", strLexemeCurrent, "", false, false, iScriptPos);
                    iScriptPos = (uint)(iScriptPos + strLexemeCurrent.Length);

                    switch (clsToken.tokentype)
                    {
                        case RToken.TokenType.RSpace:
                        case RToken.TokenType.RNewLine:
                        case RToken.TokenType.RComment:
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
            if (RToken.IsValidLexeme(lexeme + lexemeChar) &&
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


}