using System.Text.RegularExpressions;

namespace RInsight;

/// --------------------------------------------------------------------------------------------
/// <summary>
/// Represents a potential R lexeme (a string of characters that represents a valid R element).
/// For example '+', 'variableName', '123', 'functionName', 'if', 'for', ', 'return', '[[', 
/// '...', 'TRUE', 'NaN', '...', '::', '>=', '<-',  '# comment' etc.
/// The class includes a set of public properties that identify the type of lexeme; and whether
/// the lexeme is valid or invalid.
/// </summary>
/// --------------------------------------------------------------------------------------------
public class RLexeme
{

    /// <summary> The text associated with the lexeme. </summary>
    public string Text { get; }

    /// <summary> True if this lexeme is a round or curly bracket.</summary>
    public bool IsBracket => _IsBracket();

    /// <summary> True if this lexeme is a comment.<para>
    ///           Any text from a # character to the end of the line is taken to be a comment,
    ///           unless the # character is inside a quoted string. </para></summary>
    public bool IsComment => _IsComment();

    /// <summary> True if this lexeme is a complete or partial string constant.<para>
    ///           String constants are delimited by a pair of single (‘'’), double (‘"’)
    ///           or backtick ('`') quotes and can contain all other printable characters. 
    ///           Quotes and other special characters within strings are specified using escape 
    ///           sequences. </para></summary>
    public bool IsConstantString => _IsConstantString();

    /// <summary> True if this lexeme is a functional R element 
    ///           (i.e. not empty, and not a space, comment or new line). </summary>
    public bool IsElement => _IsElement();

    /// <summary> True if this lexeme is a key word ("if", "else", "repeat", "while", "function", 
    ///           "for", "in", "next", or "break").</summary>
    public bool IsKeyWord => _IsKeyWord();

    /// <summary> True if this lexeme is a new line, carriage return or new line plus carriage 
    ///           return.</summary>
    public bool IsNewLine => _IsNewLine();

    /// <summary> True if this lexeme is a valid left-hand parameter for a binary operator 
    ///           (an identifier or a constant).</summary>
    public bool IsOperatorBinaryParameterLeft => _IsOperatorBinaryParameterLeft();

    /// <summary> True if this lexeme is a valid right-hand parameter for a binary operator 
    ///           (an identifier or a constant).</summary>
    public bool IsOperatorBinaryParameterRight => _IsOperatorBinaryParameterRight();

    /// <summary> True if this lexeme is a bracket operator ("[" or "[[").</summary>
    public bool IsOperatorBrackets => _IsOperatorBrackets();

    /// <summary> True if this lexeme is a reserved operator ("*", "+", "-" etc.).</summary>
    public bool IsOperatorReserved => _IsOperatorReserved();

    /// <summary> True if this lexeme is a unary operator ("+", "-", "!", "~", "?" or "??").
    /// </summary>
    public bool IsOperatorUnary => _IsOperatorUnary();

    /// <summary> True if this lexeme is a complete or partial user-defined operator (e.g. "%>%").
    /// </summary>
    public bool IsOperatorUserDefined => _IsOperatorUserDefined();

    /// <summary> True if this lexeme is a complete or partial user-defined operator (e.g. "%>%").
    /// </summary>
    public bool IsOperatorUserDefinedComplete => _IsOperatorUserDefinedComplete();

    /// <summary> True if this lexeme is sequence of spaces (and no other characters).</summary>
    public bool IsSequenceOfSpaces => _IsSequenceOfSpaces();

    /// <summary> True if this lexeme is a complete or partial valid R syntactic name or key word.
    ///           <para>
    ///           Please note that the rules for syntactic names are actually stricter than the 
    ///           rules used in this function, but this library assumes it is parsing valid R code.
    ///           </para></summary>
    public bool IsSyntacticName => _IsSyntacticName();

    /// <summary> True if this lexeme's text is a valid lexeme (either partial or complete).
    ///           </summary>
    public bool IsValid => _IsValidLexeme();


    /// --------------------------------------------------------------------------------------------
    /// <summary>
    ///     Constructs a new lexeme with text <paramref name="text"/>.<para>
    ///     A lexeme is a string of characters that represent a potentially valid R element 
    ///     (identifier, operator, keyword, bracket etc.). </para>
    /// </summary>
    /// 
    /// <param name="text">    The text to associate with the lexeme. </param>
    /// --------------------------------------------------------------------------------------------
    public RLexeme(string text)
    {
        Text = text;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a bracket, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a bracket, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsBracket()
    {
        var brackets = new string[] { "(", ")", "{", "}" };
        return brackets.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a comment, else returns false.
    ///             <para>
    ///             Any text from a # character to the end of the line is taken to be a comment,
    ///             unless the # character is inside a quoted string. </para></summary>
    /// 
    /// <returns>   True if this lexeme's text is a comment, else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsComment()
    {
        return Regex.IsMatch(Text, "^#.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a complete or partial string 
    ///             constant, else returns false.<para>
    ///             String constants are delimited by a pair of single (‘'’), double (‘"’)
    ///             or backtick ('`') quotes and can contain all other printable characters. 
    ///             Quotes and other special characters within strings are specified using escape 
    ///             sequences. </para></summary>
    /// 
    /// <returns>   True if this lexeme's text is a complete or partial string constant,
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsConstantString()
    {
        return Regex.IsMatch(Text, "^\".*") ||
               Regex.IsMatch(Text, "^'.*") ||
               Regex.IsMatch(Text, "^`.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a functional R element 
    ///             (i.e. not empty, and not a space, comment or new line), else returns false. </summary>
    /// 
    /// <returns>   True  if this lexeme's text is a functional R element
    ///             (i.e. not a space, comment or new line), else returns false. </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsElement()
    {
        return !(_IsNewLine() || _IsSequenceOfSpaces() || _IsComment());
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a key word, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a key word, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsKeyWord()
    {
        var arrKeyWords = new string[] { "if", "else", "repeat", "while", "function", "for", "in", "next", "break" };
        return arrKeyWords.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a new line, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a new line, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsNewLine()
    {
        var arrRNewLines = new string[] { "\r", "\n", "\r\n" };
        return arrRNewLines.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a valid left-hand parameter for a binary 
    ///             operator, else returns false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a valid left-hand parameter for a binary operator, 
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorBinaryParameterLeft()
    {
        return Regex.IsMatch(Text, @"[a-zA-Z0-9_\.)\]]$") || _IsConstantString();
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a valid right-hand parameter for a binary 
    ///             operator, else returns false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a valid right-hand parameter for a binary operator, 
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorBinaryParameterRight()
    {
        return Regex.IsMatch(Text, @"^[a-zA-Z0-9_\.(\+\-\!~]") || _IsConstantString();
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a bracket operator, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a bracket operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorBrackets()
    {
        var operatorBrackets = new string[] { "[", "]", "[[", "]]" };
        return operatorBrackets.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a reserved operator, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a reserved operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorReserved()
    {
        var operators = new string[] { "::", ":::", "$", "@", "^", ":", "%%", "%/%", "%*%",
                "%o%", "%x%", "%in%", "/", "*", "+", "-", "<", ">", "<=", ">=", "==", "!=", "!",
                "&", "&&", "|", "||", "|>", "~", "->", "->>", "<-", "<<-", "=", "?", "??" };
        return operators.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a unary operator, else returns 
    ///             false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a unary operator, else returns false.
    ///             </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorUnary()
    {
        var operatorUnaries = new string[] { "+", "-", "!", "~", "?", "??" };
        return operatorUnaries.Contains(Text);
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a complete or partial  
    ///             user-defined operator, else returns false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a complete or partial  
    ///             user-defined operator, else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorUserDefined()
    {
        return Regex.IsMatch(Text, "^%.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a complete user-defined operator, else 
    ///             returns false.</summary>
    /// 
    /// <returns>   True if this lexeme's text is a complete user-defined operator, else returns 
    ///             false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsOperatorUserDefinedComplete()
    {
        return Regex.IsMatch(Text, "^%.*%$");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is sequence of spaces (and no other 
    ///             characters), else returns false. </summary>
    /// 
    /// <returns>   True  if this lexeme's text is sequence of spaces (and no other 
    ///             characters), else returns false. </returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsSequenceOfSpaces()
    {
        return (Text != "\n" && Regex.IsMatch(Text, "^ *$")) || Text == "\t";
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme's text is a complete or partial 
    ///             valid R syntactic name or key word, else returns false.<para>
    ///             Please note that the rules for syntactic names are actually stricter than 
    ///             the rules used in this function, but this library assumes it is parsing valid 
    ///             R code. </para></summary>
    /// 
    /// <returns>   True if this lexeme's text is a valid R syntactic name or key word, 
    ///             else returns false.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool _IsSyntacticName()
    {
        return Regex.IsMatch(Text, @"^[a-zA-Z0-9_\.]+$") || Regex.IsMatch(Text, "^`.*");
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns true if this lexeme is a valid lexeme (either partial or  complete), 
    ///             else returns false. </summary>
    /// 
    /// <returns>   True if this lexeme is a valid lexeme, else false. </returns>
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
                !(Text == "\r\n" || _IsConstantString()) ||  // >1 char and ends in newline
                Regex.IsMatch(Text, @".+\r$") ||             // >1 char and ends in carriage return
                Regex.IsMatch(Text, "^%.*%.+")) // a user-defined operator followed by another char
        { 
            return false;
        }

        // if string is a valid lexeme ...
        if (_IsSyntacticName() 
            || _IsOperatorReserved() || _IsOperatorBrackets() || _IsOperatorUserDefined() 
            || Text == "<<" || _IsNewLine() || Text == "," || Text == ";" 
            || _IsBracket() || _IsSequenceOfSpaces() || _IsComment())
        {
            return true;
        }

        // if the string is not covered by any of the checks above, 
        // then we assume by default, that it's not a valid lexeme
        return false;
    }
}