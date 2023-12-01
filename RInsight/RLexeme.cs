using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RInsight;

/// <summary>
/// TODO
/// </summary>
public class RLexeme {

    /// <summary>   The text associated with the lexeme. </summary>
    public string Text { get; }

    public bool IsValid => _IsValidLexeme();

    public bool IsBinaryOperatorParameter
    { get { return _IsBinaryOperatorParameter(); } }
    public bool IsBracket => _IsBracket();
    public bool IsComment => _IsComment();
    public bool IsConstantString => _IsConstantString();
    public bool IsElement => _IsElement();
    public bool IsKeyWord => _IsKeyWord();
    public bool IsNewLine => _IsNewLine();
    public bool IsSequenceOfSpaces => _IsSequenceOfSpaces();
    public bool IsSyntacticName => _IsSyntacticName();
    public bool IsOperatorBrackets => _IsOperatorBrackets();
    public bool IsOperatorUnary => _IsOperatorUnary();
    public bool IsOperatorReserved => _IsOperatorReserved();
    public bool IsOperatorUserDefined => _IsOperatorUserDefined();


    /// --------------------------------------------------------------------------------------------
    /// <summary>
    ///     Constructs a new token with lexeme <paramref name="textNew"/> and token type 
    ///     <paramref name="tokenTypeNew"/>.
    ///     <para>
    ///     A token is a string of characters that represent a valid R element, plus meta data about
    ///     the token type (identifier, operator, keyword, bracket etc.).
    ///     </para>
    /// </summary>
    /// 
    /// <param name="textNew">    The lexeme to associate with the token. </param>
    /// <param name="tokenTypeNew">  The token type (function name, key word, comment etc.). </param>
    /// --------------------------------------------------------------------------------------------
    public RLexeme(string text) {
        Text = text;
    }


    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a valid parameter for a binary 
    ///             operator, else returns false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a valid parameter for a binary operator, 
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsBinaryOperatorParameter() {
        return Regex.IsMatch(Text, @"[a-zA-Z0-9_\.)\]]$") || _IsConstantString();
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a bracket, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a bracket, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsBracket()
    {
        var brackets = new string[] { "(", ")", "{", "}" };
        return brackets.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a comment, else returns false.
    ///             <para>
    ///             Any text from a # character to the end of the line is taken to be a comment,
    ///             unless the # character is inside a quoted string. </para></summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a comment, else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsComment()
    {
        return Regex.IsMatch(Text, "^#.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a complete or partial string 
    ///             constant, else returns false.<para>
    ///             String constants are delimited by a pair of single (‘'’), double (‘"’)
    ///             or backtick ('`') quotes and can contain all other printable characters. 
    ///             Quotes and other special characters within strings are specified using escape 
    ///             sequences. </para></summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a complete or partial string constant,
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsConstantString()
    {
        return Regex.IsMatch(Text, "^\".*") ||
               Regex.IsMatch(Text, "^'.*") ||
               Regex.IsMatch(Text, "^`.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a functional R element 
    ///             (i.e. not empty, and not a space, comment or new line), else returns false. </summary>
    /// 
    /// <param name="text">   The text to check . </param>
    /// 
    /// <returns>   True  if <paramref name="text"/> is a functional R element
    ///             (i.e. not a space, comment or new line), else returns false. </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsElement()
    { // TODO make private?
        return !(_IsNewLine() || _IsSequenceOfSpaces() || _IsComment());
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a key word, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a key word, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsKeyWord()
    {
        var arrKeyWords = new string[] { "if", "else", "repeat", "while", "function", "for", "in", "next", "break" };
        return arrKeyWords.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a new line, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a new line, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsNewLine()
    { // TODO make private?
        var arrRNewLines = new string[] { "\r", "\n", "\r\n" };
        return arrRNewLines.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a bracket operator, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a bracket operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorBrackets()
    {
        var operatorBrackets = new string[] { "[", "]", "[[", "]]" };
        return operatorBrackets.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a resrved operator, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a reserved operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorReserved()
    { // TODO make private?
        var operators = new string[] { "::", ":::", "$", "@", "^", ":", "%%", "%/%", "%*%",
                "%o%", "%x%", "%in%", "/", "*", "+", "-", "<", ">", "<=", ">=", "==", "!=", "!",
                "&", "&&", "|", "||", "|>", "~", "->", "->>", "<-", "<<-", "=", "?", "??" };
        return operators.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a unary operator, else returns 
    ///             false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a unary operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorUnary()
    {
        var operatorUnaries = new string[] { "+", "-", "!", "~", "?", "??" };
        return operatorUnaries.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a complete or partial  
    ///             user-defined operator, else returns false.</summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a complete or partial  
    ///             user-defined operator, else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorUserDefined()
    { // TODO make private?
        return Regex.IsMatch(Text, "^%.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is sequence of spaces (and no other 
    ///             characters), else returns false. </summary>
    /// 
    /// <param name="text">   The text to check . </param>
    /// 
    /// <returns>   True  if <paramref name="text"/> is sequence of spaces (and no other 
    ///             characters), else returns false. </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsSequenceOfSpaces()
    {
        return Text != "\n" && Regex.IsMatch(Text, "^ *$");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="text"/> is a complete or partial 
    ///             valid R syntactic name or key word, else returns false.<para>
    ///             Please note that the rules for syntactic names are actually stricter than 
    ///             the rules used in this function, but this library assumes it is parsing valid 
    ///             R code. </para></summary>
    /// 
    /// <param name="text">   The text to check. </param>
    /// 
    /// <returns>   True if <paramref name="text"/> is a valid R syntactic name or key word, 
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsSyntacticName() {
        return Regex.IsMatch(Text, @"^[a-zA-Z0-9_\.]+$") || Regex.IsMatch(Text, "^`.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if <paramref name="lexeme"/> is a valid lexeme (either partial or 
    ///             complete), else returns false.
    ///             </summary>
    /// 
    /// <param name="lexeme">   A sequence of characters from a syntactically correct R script </param>
    /// 
    /// <returns>   True if <paramref name="lexeme"/> is a valid lexeme, else false. </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsValidLexeme()
    {
        if (Text.Length == 0)
        {
            return false;
        }

        // if string constant (starts with single quote, double quote or backtick)
        // Note: String constants are the only lexemes that can contain newlines and quotes. 
        // So if we process string constants first, then it makes checks below simpler.
        if (_IsConstantString())
        {
            // if string constant is closed and followed by another character (e.g. '"hello"\n')
            // Note: "(?<!\\)" is a Regex 'negative lookbehind'. It excludes quotes that are 
            // preceeded by a backslash.
            return !Regex.IsMatch(Text,
                                  Text[0] + @"(.|\n)*" + @"(?<!\\)" + Text[0] + @"(.|\n)+");
        }

        // if string is not a valid lexeme ...
        if (Regex.IsMatch(Text, @".+\n$") &&
                !(Text == "\r\n" || _IsConstantString()) ||   // >1 char and ends in newline
                Regex.IsMatch(Text, @".+\r$") ||           // >1 char and ends in carriage return
                Regex.IsMatch(Text, "^%.*%.+"))
        { // a user-defined operator followed by another character
            return false;
        }

        // if string is a valid lexeme ...
        if (_IsSyntacticName() || // syntactic name or reserved word
                _IsOperatorReserved() || _IsOperatorBrackets() ||
                Text == "<<" || _IsNewLine() || Text == "," || Text == ";" ||
                _IsBracket() || _IsSequenceOfSpaces() || _IsOperatorUserDefined() ||
                _IsComment())
        {
            return true;
        }

        // if the string is not covered by any of the checks above, 
        // then we assume by default, that it's not a valid lexeme
        return false;
    }
}