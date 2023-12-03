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
        //todo REndScript,
        RNewLine,
        ROperatorUnaryLeft,
        ROperatorUnaryRight,
        ROperatorBinary,
        ROperatorBracket,
        RPresentation,
        RInvalid
    }

    // TODO only allow RTokenList to change this list?
    /// <summary>   The token's children. </summary>
    public List<RToken> ChildTokens { get; set; }

    /// <summary>   The lexeme associated with the token. </summary>
    public RLexeme Lexeme { get; }

    /// <summary>   The position of the lexeme in the script from which the lexeme was extracted. </summary>
    public uint ScriptPos { get; }

    /// <summary>   The token type (function name, key word, comment etc.).  </summary>
    public TokenType Tokentype { get; }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    ///     Constructs a new token with lexeme <paramref name="textNew"/> and token type 
    ///     <paramref name="tokenType"/>.
    ///     <para>
    ///     A token is a string of characters that represent a valid R element, plus meta data about
    ///     the token type (identifier, operator, keyword, bracket etc.).
    ///     </para>
    /// </summary>
    /// 
    /// <param name="textNew">    The lexeme to associate with the token. </param>
    /// <param name="tokenType">  The token type (function name, key word, comment etc.). </param>
    /// --------------------------------------------------------------------------------------------
    public RToken(RLexeme lexeme, TokenType tokenType)
    {
        ChildTokens = new List<RToken>();
        Lexeme = lexeme;
        ScriptPos = 0;
        Tokentype = tokenType;
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
                  bool lexemePrevOnSameLine, bool lexemeNextOnSameLine, uint scriptPosNew, bool statementHasOpenBrackets, bool statementContainsElement)
    {
        if (string.IsNullOrEmpty(lexemeCurrent.Text))
        {
            throw new Exception("Lexeme has no text.");
        }

        Lexeme = lexemeCurrent;
        ChildTokens = new List<RToken>();
        ScriptPos = scriptPosNew;

        if (lexemeCurrent.IsKeyWord)
        {
            Tokentype = TokenType.RKeyWord;                // reserved key word (e.g. if, else etc.)
        }
        else if (lexemeCurrent.IsSyntacticName)
        {
            if (lexemeNext.Text == "(" && lexemeNextOnSameLine)
            {
                Tokentype = TokenType.RFunctionName;       // function name
            }
            else
            {
                Tokentype = TokenType.RSyntacticName;      // syntactic name
            }
        }
        else if (lexemeCurrent.IsComment)
        {
            Tokentype = TokenType.RComment;             // comment (starts with '#*')
        }
        else if (lexemeCurrent.IsConstantString)
        {
            Tokentype = TokenType.RConstantString;        // string literal (starts with single or double quote)
        }
        else if (lexemeCurrent.IsNewLine)
        {
            if (!statementContainsElement
                || statementHasOpenBrackets
                || lexemePrev.IsOperatorUserDefined 
                || (lexemePrev.IsOperatorReserved && lexemePrev.Text != "~"))
            {
                Tokentype = TokenType.RNewLine;               // new line (e.g. '\n')
            }
            else
            {
                Tokentype = TokenType.REndStatement;
            }
        }
        else if (lexemeCurrent.Text == ";")
        {
            Tokentype = TokenType.REndStatement;                    // end statement
        }
        else if (lexemeCurrent.Text == ",")
        {
            Tokentype = TokenType.RSeparator;                    // parameter separator
        }
        else if (lexemeCurrent.IsSequenceOfSpaces)
        {     // sequence of spaces (needs to be after separator check, 
            Tokentype = TokenType.RSpace;              // else linefeed is recognised as space)
        }
        else if (lexemeCurrent.IsBracket)
        {              // bracket (e.g. '{')
            if (lexemeCurrent.Text == "}")
            {
                //todo Tokentype = TokenType.REndScript;
                Tokentype = TokenType.REndStatement;
            }
            else
            {
                Tokentype = TokenType.RBracket;
            }
        }
        else if (lexemeCurrent.IsOperatorBrackets)
        {
            Tokentype = TokenType.ROperatorBracket;      // bracket operator (e.g. '[')
        }
        else if (lexemeCurrent.IsOperatorUnary &&
                   (string.IsNullOrEmpty(lexemePrev.Text) ||
                    !lexemePrev.IsOperatorBinaryParameterLeft ||
                    !lexemePrevOnSameLine))
        {
            Tokentype = TokenType.ROperatorUnaryRight;      // unary right operator (e.g. '!x')
        }
        else if (lexemeCurrent.Text == "~" &&
                 lexemePrev.IsOperatorBinaryParameterLeft &&
                 (!lexemeNext.IsOperatorBinaryParameterRight || !lexemeNextOnSameLine))
        {
            Tokentype = TokenType.ROperatorUnaryLeft;                 // unary left operator (e.g. x~)
        }
        else if (lexemeCurrent.IsOperatorReserved || lexemeCurrent.IsOperatorUserDefinedComplete)
        {
            Tokentype = TokenType.ROperatorBinary;    // binary operator (e.g. '+')
        }
        else
        {
            Tokentype = TokenType.RInvalid;
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
        var token = new RToken(Lexeme, Tokentype);

        foreach (RToken clsTokenChild in ChildTokens)
        {
            if (clsTokenChild is null)
            {
                throw new Exception("Token has illegal empty child.");
            }
            token.ChildTokens.Add(clsTokenChild.CloneMe());
        }

        return token;
    }

}