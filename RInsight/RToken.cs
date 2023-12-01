namespace RInsight;

/// <summary>
/// TODO
/// </summary>
public class RToken
{

    /// <summary>   The different types of R element (function name, key word, comment etc.) 
    ///             that the token may represent. </summary>
    public enum TokenType
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

    /// <summary>   The token's children. </summary>
    public List<RToken> childTokens = new();

    /// <summary>   The lexeme associated with the token. </summary>
    public RLexeme Lexeme;

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
    public RToken(RLexeme lexeme, TokenType tokenTypeNew)
    {
        Lexeme = lexeme;
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
    public RToken(RLexeme lexemePrev, RLexeme lexemeCurrent, RLexeme lexemeNext,
                  bool lexemePrevOnSameLine, bool lexemeNextOnSameLine, uint scriptPosNew)
    {
        if (string.IsNullOrEmpty(lexemeCurrent.Text))
        {
            throw new Exception("Lexeme has no text.");
        }

        Lexeme = lexemeCurrent;
        scriptPos = scriptPosNew;

        if (lexemeCurrent.IsKeyWord)
        {
            tokentype = TokenType.RKeyWord;                // reserved key word (e.g. if, else etc.)
        }
        else if (lexemeCurrent.IsSyntacticName)
        {
            if (lexemeNext.Text == "(" && lexemeNextOnSameLine)
            {
                tokentype = TokenType.RFunctionName;       // function name
            }
            else
            {
                tokentype = TokenType.RSyntacticName;      // syntactic name
            }
        }
        else if (lexemeCurrent.IsComment)
        {
            tokentype = TokenType.RComment;             // comment (starts with '#*')
        }
        else if (lexemeCurrent.IsConstantString)
        {
            tokentype = TokenType.RConstantString;        // string literal (starts with single or double quote)
        }
        else if (lexemeCurrent.IsNewLine)
        {
            tokentype = TokenType.RNewLine;               // new line (e.g. '\n')
        }
        else if (lexemeCurrent.Text == ";")
        {
            tokentype = TokenType.REndStatement;                    // end statement
        }
        else if (lexemeCurrent.Text == ",")
        {
            tokentype = TokenType.RSeparator;                    // parameter separator
        }
        else if (lexemeCurrent.IsSequenceOfSpaces)
        {     // sequence of spaces (needs to be after separator check, 
            tokentype = TokenType.RSpace;              // else linefeed is recognised as space)
        }
        else if (lexemeCurrent.IsBracket)
        {              // bracket (e.g. '{')
            if (lexemeCurrent.Text == "}")
            {
                tokentype = TokenType.REndScript;
            }
            else
            {
                tokentype = TokenType.RBracket;
            }
        }
        else if (lexemeCurrent.IsOperatorBrackets)
        {
            tokentype = TokenType.ROperatorBracket;      // bracket operator (e.g. '[')
        }
        else if (lexemeCurrent.IsOperatorUnary &&
                   (string.IsNullOrEmpty(lexemePrev.Text) ||
                    !lexemePrev.IsOperatorBinaryParameterLeft ||
                    !lexemePrevOnSameLine))
        {
            tokentype = TokenType.ROperatorUnaryRight;      // unary right operator (e.g. '!x')
        }
        else if (lexemeCurrent.Text == "~" &&
                 lexemePrev.IsOperatorBinaryParameterLeft &&
                 (!lexemeNext.IsOperatorBinaryParameterRight || !lexemeNextOnSameLine))
        {
            tokentype = TokenType.ROperatorUnaryLeft;                 // unary left operator (e.g. x~)
        }
        else if (lexemeCurrent.IsOperatorReserved || lexemeCurrent.IsOperatorUserDefinedComplete)
        {
            tokentype = TokenType.ROperatorBinary;    // binary operator (e.g. '+')
        }
        else
        {
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
    public RToken CloneMe()
    {
        var token = new RToken(Lexeme, tokentype);

        foreach (RToken clsTokenChild in childTokens)
        {
            if (clsTokenChild is null)
            {
                throw new Exception("Token has illegal empty child.");
            }
            token.childTokens.Add(clsTokenChild.CloneMe());
        }

        return token;
    }

}