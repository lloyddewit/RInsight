using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

namespace RInsight;

/// <summary>
/// TODO
/// </summary>
public class RToken {

    /// <summary>   The different types of R element (function name, key word, comment etc.) 
    ///             that the token may represent. </summary>
    public enum TokenType {
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

    /// <summary>   The token's children. </summary>
    public List<RToken> childTokens = new ();

    /// <summary>   The lexeme associated with the token. </summary>
    public string text;

    /// <summary>   The token type (function name, key word, comment etc.).  </summary>
    public TokenType tokentype;

    /// <summary>   The position of the lexeme in the script from which the lexeme was extracted. </summary>
    public uint scriptPos;

    /// <summary>   The current state of the token parsing. </summary>
    private enum typTokenState
    {
        WaitingForOpenCondition,
        WaitingForCloseCondition,
        WaitingForStartScript,
        WaitingForEndScript
    }

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
    public RToken(string textNew, TokenType tokenTypeNew) {
        text = textNew;
        tokentype = tokenTypeNew;
    }


    /// --------------------------------------------------------------------------------------------
    /// <summary>
    ///     Constructs a token from <paramref name="lexemeCurrent"/>. 
    ///     <para>
    ///     A token is a string of characters that represent a valid R element, plus meta data about
    ///     the token type (identifier, operator, keyword, bracket etc.).
    ///     </para><para>
    ///     <paramref name="lexemePrev"/> and <paramref name="lexemeNext"/> are needed
    ///     to correctly identify if <paramref name="lexemeCurrent"/> is a unary or binary
    ///     operator.</para>
    /// </summary>
    /// 
    /// <param name="lexemePrev">         The non-space lexeme immediately to the left of
    ///                                      <paramref name="lexemeCurrent"/>. </param>
    /// <param name="lexemeCurrent">      The lexeme to convert to a token. </param>
    /// <param name="lexemeNext">         The non-space lexeme immediately to the right of
    ///                                      <paramref name="lexemeCurrent"/>. </param>
    /// <param name="lexemeNextOnSameLine"> True if <paramref name="lexemeNext"/> is on the 
    ///                                      same line as <paramref name="lexemeCurrent"/>. </param>
    /// <param name="scriptPosNew">         The position of <paramref name="lexemeCurrent"/> in
    ///                                      the script from which the lexeme was extracted. </param>
    /// 
    /// --------------------------------------------------------------------------------------------
    public RToken(string lexemePrev, string lexemeCurrent, string lexemeNext, 
                  bool lexemePrevOnSameLine, bool lexemeNextOnSameLine, uint scriptPosNew) {
        if (string.IsNullOrEmpty(lexemeCurrent)) {
            throw new Exception("Lexeme has no text.");
        }

        text = lexemeCurrent;
        scriptPos = scriptPosNew;

        if (IsKeyWord(lexemeCurrent)) {
            tokentype = TokenType.RKeyWord;                // reserved key word (e.g. if, else etc.)
        } else if (IsSyntacticName(lexemeCurrent)) {
            if (lexemeNext == "(" && lexemeNextOnSameLine) {
                tokentype = TokenType.RFunctionName;       // function name
            } else {
                tokentype = TokenType.RSyntacticName;      // syntactic name
            }
        } else if (IsComment(lexemeCurrent)) {
            tokentype = TokenType.RComment;             // comment (starts with '#*')
        } else if (IsConstantString(lexemeCurrent)) {
            tokentype = TokenType.RConstantString;        // string literal (starts with single or double quote)
        } else if (IsNewLine(lexemeCurrent)) {
            tokentype = TokenType.RNewLine;               // new line (e.g. '\n')
        } else if (lexemeCurrent == ";") {
            tokentype = TokenType.REndStatement;                    // end statement
        } else if (lexemeCurrent == ",") {
            tokentype = TokenType.RSeparator;                    // parameter separator
        } else if (IsSequenceOfSpaces(lexemeCurrent)) {     // sequence of spaces (needs to be after separator check, 
            tokentype = TokenType.RSpace;              // else linefeed is recognised as space)
        } else if (IsBracket(lexemeCurrent)) {              // bracket (e.g. '{')
            if (lexemeCurrent == "}") {
                tokentype = TokenType.REndScript;
            } else {
                tokentype = TokenType.RBracket;
            }
        } else if (IsOperatorBrackets(lexemeCurrent)) {
            tokentype = TokenType.ROperatorBracket;      // bracket operator (e.g. '[')
        } else if (IsOperatorUnary(lexemeCurrent) && 
                   (string.IsNullOrEmpty(lexemePrev) 
                    || !IsBinaryOperatorParameter(lexemePrev) || 
                    !lexemePrevOnSameLine)) {
            tokentype = TokenType.ROperatorUnaryRight;      // unary right operator (e.g. '!x')
        } else if (lexemeCurrent == "~" && (string.IsNullOrEmpty(lexemeNext) || !lexemeNextOnSameLine || !(Regex.IsMatch(lexemeNext, @"^[a-zA-Z0-9_\.(\+\-\!~]") || IsBinaryOperatorParameter(lexemeNext)))) {
            tokentype = TokenType.ROperatorUnaryLeft;                 // unary left operator (e.g. x~)
        } else if (IsOperatorReserved(lexemeCurrent) || Regex.IsMatch(lexemeCurrent, "^%.*%$")) {
            tokentype = TokenType.ROperatorBinary;    // binary operator (e.g. '+')
        } else {
            tokentype = TokenType.RInvalid;
        }
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Creates and returns a clone of this object. </summary>
    /// 
    /// <exception cref="Exception">    Thrown when the object has an empty child token. </exception>
    /// 
    /// <returns>   A clone of this object. </returns>
    /// --------------------------------------------------------------------------------------------
    public RToken CloneMe() {
        var token = new RToken(text, tokentype);

        foreach (RToken clsTokenChild in childTokens) {
            if (clsTokenChild is null) {
                throw new Exception("Token has illegal empty child.");
            }
            token.childTokens.Add(clsTokenChild.CloneMe());
        }

        return token;
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
    public static List<RToken>? GetLstTokens(List<string> lstLexemes)
    {

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
    /// <summary>   Returns true if <paramref name="lexeme"/> is a valid lexeme (either partial or 
    ///             complete), else returns false.
    ///             </summary>
    /// 
    /// <param name="lexeme">   A sequence of characters from a syntactically correct R script </param>
    /// 
    /// <returns>   True if <paramref name="lexeme"/> is a valid lexeme, else false. </returns>
    /// --------------------------------------------------------------------------------------------
    public static bool IsValidLexeme(string lexeme) {
        if (lexeme.Length == 0) {
            return false;
        }

        // if string constant (starts with single quote, double quote or backtick)
        // Note: String constants are the only lexemes that can contain newlines and quotes. 
        // So if we process string constants first, then it makes checks below simpler.
        if (IsConstantString(lexeme)) {
            // if string constant is closed and followed by another character (e.g. '"hello"\n')
            // Note: "(?<!\\)" is a Regex 'negative lookbehind'. It excludes quotes that are 
            // preceeded by a backslash.
            return !Regex.IsMatch(lexeme, 
                                  lexeme[0] + @"(.|\n)*" + @"(?<!\\)" + lexeme[0] + @"(.|\n)+");
        }

        // if string is not a valid lexeme ...
        if (Regex.IsMatch(lexeme, @".+\n$") && 
                !(lexeme == "\r\n" || IsConstantString(lexeme)) ||   // >1 char and ends in newline
                Regex.IsMatch(lexeme, @".+\r$") ||           // >1 char and ends in carriage return
                Regex.IsMatch(lexeme, "^%.*%.+")) { // a user-defined operator followed by another character
            return false;
        }

        // if string is a valid lexeme ...
        if (IsSyntacticName(lexeme) || // syntactic name or reserved word
                IsOperatorReserved(lexeme) || IsOperatorBrackets(lexeme) || 
                lexeme == "<<" || IsNewLine(lexeme) || lexeme == "," || lexeme == ";" || 
                IsBracket(lexeme) || IsSequenceOfSpaces(lexeme) || IsOperatorUserDefined(lexeme) ||
                IsComment(lexeme)) {
            return true;
        }

        // if the string is not covered by any of the checks above, 
        // then we assume by default, that it's not a valid lexeme
        return false;
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
    private static bool IsBinaryOperatorParameter(string text) {
        return Regex.IsMatch(text, @"[a-zA-Z0-9_\.)\]]$") || IsConstantString(text);
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
    private static bool IsSyntacticName(string text) {
        return Regex.IsMatch(text, @"^[a-zA-Z0-9_\.]+$") || Regex.IsMatch(text, "^`.*");
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
    private static bool IsConstantString(string text) {
        return Regex.IsMatch(text, "^\".*") || 
               Regex.IsMatch(text, "^'.*") || 
               Regex.IsMatch(text, "^`.*");
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
    private static bool IsComment(string text) {
        return Regex.IsMatch(text, "^#.*");
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
    private static bool IsSequenceOfSpaces(string text) {
        return text != "\n" && Regex.IsMatch(text, "^ *$");
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
    public static bool IsElement(string text) { // TODO make private?
        return !(IsNewLine(text) || IsSequenceOfSpaces(text) || IsComment(text));
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
    public static bool IsOperatorUserDefined(string text) { // TODO make private?
        return Regex.IsMatch(text, "^%.*");
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
    public static bool IsOperatorReserved(string text) { // TODO make private?
        var operators = new string[] { "::", ":::", "$", "@", "^", ":", "%%", "%/%", "%*%", 
                "%o%", "%x%", "%in%", "/", "*", "+", "-", "<", ">", "<=", ">=", "==", "!=", "!", 
                "&", "&&", "|", "||", "|>", "~", "->", "->>", "<-", "<<-", "=", "?", "??" };
        return operators.Contains(text);
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
    private static bool IsOperatorBrackets(string text) {
        var operatorBrackets = new string[] { "[", "]", "[[", "]]" };
        return operatorBrackets.Contains(text);
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
    private static bool IsOperatorUnary(string text) {
        var operatorUnaries = new string[] { "+", "-", "!", "~", "?", "??" };
        return operatorUnaries.Contains(text);
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
    private static bool IsBracket(string text) {
        var brackets = new string[] { "(", ")", "{", "}" };
        return brackets.Contains(text);
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
    public static bool IsNewLine(string text) { // TODO make private?
        var arrRNewLines = new string[] { "\r", "\n", "\r\n" };
        return arrRNewLines.Contains(text);
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
    private static bool IsKeyWord(string text) {
        var arrKeyWords = new string[] { "if", "else", "repeat", "while", "function", "for", "in", "next", "break" };
        return arrKeyWords.Contains(text);
    }

}