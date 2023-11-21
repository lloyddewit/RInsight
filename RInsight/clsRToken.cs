using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

namespace RInsight;

public class clsRToken
{
    /// <summary>   The different types of R element (function name, key word, comment etc.) 
///             that the token may represent. </summary>
    public enum typToken
    {
        RSyntacticName,
        RFunctionName,
        RKeyWord,
        RConstantString,
        RComment,
        RSpace,
        RBracket,
        RSeparator,
        REndStatement,
        REndScript,
        RNewLine,
        ROperatorUnaryLeft,
        ROperatorUnaryRight,
        ROperatorBinary,
        ROperatorBracket,
        RPresentation,
        RInvalid
    }

    /// <summary>   The lexeme associated with the token. </summary>
    public string strTxt;

    /// <summary>   The position of the lexeme in the script from which the lexeme was extracted. </summary>
    public uint iScriptPos;

    /// <summary>   The token type (function name, key word, comment etc.).  </summary>
    public typToken enuToken;

    /// <summary>   The token's children. </summary>
    public List<clsRToken> lstTokens = new List<clsRToken>();

    /// --------------------------------------------------------------------------------------------
/// <summary>
///     Constructs a new token with lexeme <paramref name="strTxtNew"/> and token type 
///     <paramref name="enuTokenNew"/>.
///     <para>
///     A token is a string of characters that represent a valid R element, plus meta data about
///     the token type (identifier, operator, keyword, bracket etc.).
///     </para>
/// </summary>
/// 
/// <param name="strTxtNew">    The lexeme to associate with the token. </param>
/// <param name="enuTokenNew">  The token type (function name, key word, comment etc.). </param>
/// --------------------------------------------------------------------------------------------
    public clsRToken(string strTxtNew, typToken enuTokenNew)
    {
        strTxt = strTxtNew;
        enuToken = enuTokenNew;
    }


    /// --------------------------------------------------------------------------------------------
/// <summary>
///     Constructs a token from <paramref name="strLexemeCurrent"/>. 
///     <para>
///     A token is a string of characters that represent a valid R element, plus meta data about
///     the token type (identifier, operator, keyword, bracket etc.).
///     </para><para>
///     <paramref name="strLexemePrev"/> and <paramref name="strLexemeNext"/> are needed
///     to correctly identify if <paramref name="strLexemeCurrent"/> is a unary or binary
///     operator.</para>
/// </summary>
/// 
/// <param name="strLexemePrev">         The non-space lexeme immediately to the left of
///                                      <paramref name="strLexemeCurrent"/>. </param>
/// <param name="strLexemeCurrent">      The lexeme to convert to a token. </param>
/// <param name="strLexemeNext">         The non-space lexeme immediately to the right of
///                                      <paramref name="strLexemeCurrent"/>. </param>
/// <param name="bLexemeNextOnSameLine"> True if <paramref name="strLexemeNext"/> is on the 
///                                      same line as <paramref name="strLexemeCurrent"/>. </param>
/// 
/// --------------------------------------------------------------------------------------------
    public clsRToken(string strLexemePrev, string strLexemeCurrent, string strLexemeNext, bool bLexemePrevOnSameLine, bool bLexemeNextOnSameLine, uint iScriptPosNew)
    {
        if (string.IsNullOrEmpty(strLexemeCurrent))
        {
            return;
        }

        strTxt = strLexemeCurrent;
        iScriptPos = iScriptPosNew;

        if (IsKeyWord(strLexemeCurrent))                   // reserved key word (e.g. if, else etc.)
        {
            enuToken = typToken.RKeyWord;
        }
        else if (IsSyntacticName(strLexemeCurrent))
        {
            if (strLexemeNext == "(" && bLexemeNextOnSameLine)
            {
                enuToken = typToken.RFunctionName;   // function name
            }
            else
            {
                enuToken = typToken.RSyntacticName;
            }  // syntactic name
        }
        else if (IsComment(strLexemeCurrent))               // comment (starts with '#*')
        {
            enuToken = typToken.RComment;
        }
        else if (IsConstantString(strLexemeCurrent))        // string literal (starts with single or double quote)
        {
            enuToken = typToken.RConstantString;
        }
        else if (IsNewLine(strLexemeCurrent))               // new line (e.g. '\n')
        {
            enuToken = typToken.RNewLine;
        }
        else if (strLexemeCurrent == ";")                    // end statement
        {
            enuToken = typToken.REndStatement;
        }
        else if (strLexemeCurrent == ",")                    // parameter separator
        {
            enuToken = typToken.RSeparator;
        }
        else if (IsSequenceOfSpaces(strLexemeCurrent))      // sequence of spaces (needs to be after separator check, 
        {
            enuToken = typToken.RSpace;              // else linefeed is recognised as space)
        }
        else if (IsBracket(strLexemeCurrent))               // bracket (e.g. '{')
        {
            if (strLexemeCurrent == "}")
            {
                enuToken = typToken.REndScript;
            }
            else
            {
                enuToken = typToken.RBracket;
            }
        }
        else if (IsOperatorBrackets(strLexemeCurrent))      // bracket operator (e.g. '[')
        {
            enuToken = typToken.ROperatorBracket;
        }
        else if (IsOperatorUnary(strLexemeCurrent) && (string.IsNullOrEmpty(strLexemePrev) || !IsBinaryOperatorParameter(strLexemePrev) || !bLexemePrevOnSameLine))      // unary right operator (e.g. '!x')
        {
            enuToken = typToken.ROperatorUnaryRight;
        }
        else if (strLexemeCurrent == "~" && (string.IsNullOrEmpty(strLexemeNext) || !bLexemeNextOnSameLine || !(Regex.IsMatch(strLexemeNext, @"^[a-zA-Z0-9_\.(\+\-\!~]") || IsBinaryOperatorParameter(strLexemeNext))))                 // unary left operator (e.g. x~)

        {
            enuToken = typToken.ROperatorUnaryLeft;
        }
        else if (IsOperatorReserved(strLexemeCurrent) || Regex.IsMatch(strLexemeCurrent, "^%.*%$"))    // binary operator (e.g. '+')
        {
            enuToken = typToken.ROperatorBinary;
        }
        else
        {
            enuToken = typToken.RInvalid;
        }

    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Creates and returns a clone of this object. </summary>
/// 
/// <exception cref="Exception">    Thrown when the object has an empty child token. </exception>
/// 
/// <returns>   A clone of this object. </returns>
/// --------------------------------------------------------------------------------------------
    public clsRToken CloneMe()
    {
        var clsToken = new clsRToken(strTxt, enuToken);

        foreach (clsRToken clsTokenChild in lstTokens)
        {
            if (clsTokenChild == null)
            {
                throw new Exception("Token has illegal empty child.");
            }
            clsToken.lstTokens.Add(clsTokenChild.CloneMe());
        }

        return clsToken;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a valid lexeme (either partial or 
///             complete), else returns false.
///             </summary>
/// 
/// <param name="strTxt">   A sequence of characters from a syntactically correct R script </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a valid lexeme, else  false. </returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsValidLexeme(string strTxt)
    {

        if (string.IsNullOrEmpty(strTxt))
        {
            return false;
        }

        // if string constant (starts with single, double or backtick)
        // Note: String constants are the only lexemes that can contain newlines and quotes. 
        // So if we process string constants first, then it makes checks below simpler.
        if (IsConstantString(strTxt))
        {
            // if string constant is closed and followed by another character (e.g. '"hello"\n')
            // Note: "(?<!\\)" is a Regex 'negative lookbehind'. It excludes quotes that are 
            // preceeded by a backslash.
            if (Regex.IsMatch(strTxt, strTxt[0] + @"(.|\n)*" + @"(?<!\\)" + strTxt[0] + @"(.|\n)+"))
            {
                return false;
            }
            return true;
        }

        // if string is not a valid lexeme ...
        if (Regex.IsMatch(strTxt, @".+\n$") && !((strTxt ?? "") == Constants.vbCrLf || IsConstantString(strTxt)) || Regex.IsMatch(strTxt, @".+\r$") || Regex.IsMatch(strTxt, "^%.*%.+"))                  // string is >1 char and ends in newline

        // string is >1 char and ends in carriage return
        // string is a user-defined operator followed by another character
        {
            return false;
        }

        // if string is a valid lexeme ...
        if (IsSyntacticName(strTxt) || IsOperatorReserved(strTxt) || IsOperatorBrackets(strTxt) || strTxt == "<<" || IsNewLine(strTxt) || strTxt == "," || strTxt == ";" || IsBracket(strTxt) || IsSequenceOfSpaces(strTxt) || IsOperatorUserDefined(strTxt) || IsComment(strTxt))                    // syntactic name or reserved word
                                                                                                                                                                                                                                                                                                      // operator (e.g. '+')
                                                                                                                                                                                                                                                                                                      // bracket operator (e.g. '[')
                                                                                                                                                                                                                                                                                                      // partial operator (e.g. ':')
                                                                                                                                                                                                                                                                                                      // newlines (e.g. '\n')
                                                                                                                                                                                                                                                                                                      // parameter separator or end statement
                                                                                                                                                                                                                                                                                                      // bracket (e.g. '{')
                                                                                                                                                                                                                                                                                                      // sequence of spaces
                                                                                                                                                                                                                                                                                                      // user-defined operator (starts with '%*')
                                                                                                                                                                                                                                                                                                      // comment (starts with '#*')
        {
            return true;
        }

        // if the string is not covered by any of the checks above, 
        // then we assume by default, that it's not a valid lexeme
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a valid parameter for a binary 
///             operator, else returns false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a valid parameter for a binary operator, 
///             else returns false.</returns>
/// --------------------------------------------------------------------------------------------
    private bool IsBinaryOperatorParameter(string strTxt)
    {
        return Regex.IsMatch(strTxt, @"[a-zA-Z0-9_\.)\]]$") || IsConstantString(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a complete or partial 
///             valid R syntactic name or key word, else returns false.<para>
///             Please note that the rules for syntactic names are actually stricter than 
///             the rules used in this function, but this library assumes it is parsing valid 
///             R code. </para></summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a valid R syntactic name or key word, 
///             else returns false.</returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsSyntacticName(string strTxt)
    {
        if (string.IsNullOrEmpty(strTxt))
        {
            return false;
        }
        return Regex.IsMatch(strTxt, @"^[a-zA-Z0-9_\.]+$") || Regex.IsMatch(strTxt, "^`.*");
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a complete or partial string 
///             constant, else returns false.<para>
///             String constants are delimited by a pair of single (‘'’), double (‘"’)
///             or backtick ('`') quotes and can contain all other printable characters. 
///             Quotes and other special characters within strings are specified using escape 
///             sequences. </para></summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a complete or partial string constant,
///             else returns false.</returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsConstantString(string strTxt)
    {
        if (!string.IsNullOrEmpty(strTxt) && (Regex.IsMatch(strTxt, "^\".*") || Regex.IsMatch(strTxt, "^'.*") || Regex.IsMatch(strTxt, "^`.*")))
        {
            return true;
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a comment, else returns false.
///             <para>
///             Any text from a # character to the end of the line is taken to be a comment,
///             unless the # character is inside a quoted string. </para></summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a comment, else returns false.</returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsComment(string strTxt)
    {
        if (!string.IsNullOrEmpty(strTxt) && Regex.IsMatch(strTxt, "^#.*"))
        {
            return true;
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is sequence of spaces (and no other 
///             characters), else returns false. </summary>
/// 
/// <param name="strTxt">   The text to check . </param>
/// 
/// <returns>   True  if <paramref name="strTxt"/> is sequence of spaces (and no other 
///             characters), else returns false. </returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsSequenceOfSpaces(string strTxt) // TODO make private?
    {
        if (!string.IsNullOrEmpty(strTxt) && !((strTxt ?? "") == Constants.vbLf) && Regex.IsMatch(strTxt, "^ *$"))
        {
            return true;
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a functional R element 
///             (i.e. not empty, and not a space, comment or new line), else returns false. </summary>
/// 
/// <param name="strTxt">   The text to check . </param>
/// 
/// <returns>   True  if <paramref name="strTxt"/> is a functional R element
///             (i.e. not a space, comment or new line), else returns false. </returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsElement(string strTxt) // TODO make private?
    {
        if (!(string.IsNullOrEmpty(strTxt) || IsNewLine(strTxt) || IsSequenceOfSpaces(strTxt) || IsComment(strTxt)))
        {
            return true;
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a complete or partial  
///             user-defined operator, else returns false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a complete or partial  
///             user-defined operator, else returns false.</returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsOperatorUserDefined(string strTxt)
    {
        if (!string.IsNullOrEmpty(strTxt) && Regex.IsMatch(strTxt, "^%.*"))
        {
            return true;
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a resrved operator, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a reserved operator, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsOperatorReserved(string strTxt) // TODO make private?
    {
        string[] arrROperators = new string[] { "::", ":::", "$", "@", "^", ":", "%%", "%/%", "%*%", "%o%", "%x%", "%in%", "/", "*", "+", "-", "<", ">", "<=", ">=", "==", "!=", "!", "&", "&&", "|", "||", "|>", "~", "->", "->>", "<-", "<<-", "=", "?", "??" };
        return arrROperators.Contains(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a bracket operator, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a bracket operator, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsOperatorBrackets(string strTxt)
    {
        string[] arrROperatorBrackets = new string[] { "[", "]", "[[", "]]" };
        return arrROperatorBrackets.Contains(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a unary operator, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a unary operator, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsOperatorUnary(string strTxt)
    {
        string[] arrROperatorUnary = new string[] { "+", "-", "!", "~", "?", "??" };
        return arrROperatorUnary.Contains(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a bracket, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a bracket, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsBracket(string strTxt)
    {
        string[] arrRBrackets = new string[] { "(", ")", "{", "}" };
        return arrRBrackets.Contains(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a new line, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a new line, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    public static bool IsNewLine(string strTxt)
    {
        string[] arrRNewLines = new string[] { Constants.vbCr, Constants.vbLf, Constants.vbCrLf };
        return arrRNewLines.Contains(strTxt);
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns true if <paramref name="strTxt"/> is a key word, else returns 
///             false.</summary>
/// 
/// <param name="strTxt">   The text to check. </param>
/// 
/// <returns>   True if <paramref name="strTxt"/> is a key word, else returns false.
///             </returns>
/// --------------------------------------------------------------------------------------------
    private static bool IsKeyWord(string strTxt)
    {
        string[] arrKeyWords = new string[] { "if", "else", "repeat", "while", "function", "for", "in", "next", "break" };
        return arrKeyWords.Contains(strTxt);
    }

}