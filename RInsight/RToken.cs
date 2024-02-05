namespace RInsight;

/// ----------------------------------------------------------------------------------------------
/// <summary>
/// Represents an R token. This consists of the token's lexeme (a string of characters that 
/// represents a valid R element) and meta data about the lexeme. The meta data includes the token 
/// type (function name, key word, comment etc.) and the token's children (if any). It also 
/// contains the token's position in the script from which the token was extracted. <para>  
/// Tokens can be structured into recursive trees to represent an entire R statement. For example, 
/// the token tree for the R statement "x <- 1 + 2" is: </para><para>
/// <-
/// ..x
/// ..+
/// ....1
/// ....2</para>
/// </summary>
/// ----------------------------------------------------------------------------------------------
public class RToken
{

    /// <summary> The different types of R element (function name, key word, comment etc.) 
    ///           that the token may represent. </summary>
    public enum TokenTypes
    {
        RBracket,
        RComment,
        RConstantString,
        REmpty,
        REndStatement,
        RFunctionName,
        RKeyWord,
        RNewLine,
        ROperatorBinary,
        ROperatorBracket,
        ROperatorUnaryLeft,
        ROperatorUnaryRight,
        RPresentation,
        RSeparator,
        RSpace,
        RSyntacticName,
    }

    /// <summary> The token's children. </summary>
    public List<RToken> ChildTokens { get; internal set; }

    /// <summary>
    /// True if the token is for human readability (e.g. space, comments etc.), rather
    /// than functionality.
    /// </summary>
    public bool IsPresentation => GetIsPresentation();

    /// <summary> The lexeme associated with the token. </summary>
    public RLexeme Lexeme { get; }

    /// <summary>
    /// The start position in the script of the statement associated with this token.
    /// </summary>
    public uint ScriptPosStartStatement => GetPosStartStatement();

    /// <summary>
    /// The start position in the script of the statement associated with this token.
    /// </summary>
    public uint ScriptPosEndStatement => GetPosEndStatement();

    /// <summary>   The token type (function name, key word, comment etc.).  </summary>
    public TokenTypes TokenType { get; private set; }

    /// <summary> The position of the lexeme in the script from which the lexeme was extracted. todo reorder and make read only</summary>
    public uint ScriptPos;

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
    /// <param name="lexeme">    The lexeme to associate with the token. </param>
    /// <param name="scriptPos"> The position of the lexeme in the script
    /// <param name="tokenType"> The token type (function name, key word, comment etc.). </param>
    /// --------------------------------------------------------------------------------------------
    public RToken(RLexeme lexeme, uint scriptPos, TokenTypes tokenType)
    {
        ChildTokens = new List<RToken>();
        Lexeme = lexeme;
        ScriptPos = scriptPos;
        TokenType = tokenType;
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
    /// <param name="lexemePrev">               The non-space lexeme immediately to the left of
    ///                                         <paramref name="lexemeCurrent"/>. </param>
    /// <param name="lexemeCurrent">            The lexeme to convert to a token. </param>
    /// <param name="lexemeNext">               The non-space lexeme immediately to the right of
    ///                                         <paramref name="lexemeCurrent"/>. </param>
    /// <param name="lexemePrevOnSameLine">     True if <paramref name="lexemePrev"/> is on the
    ///                                         same line as <paramref name="lexemeCurrent"/>. </param>
    /// <param name="lexemeNextOnSameLine">     True if <paramref name="lexemeNext"/> is on the 
    ///                                         same line as <paramref name="lexemeCurrent"/>. </param>
    /// <param name="scriptPosNew">             The position of <paramref name="lexemeCurrent"/> in
    ///                                         the script from which the lexeme was extracted. </param>
    /// <param name="statementHasOpenBrackets"> True if the statement containing this lexeme has 
    ///                                         unclosed brackets. </param>
    /// <param name="statementContainsElement"> True if the statement containing this lexeme has at 
    ///                                         least one element. </param>
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
            TokenType = TokenTypes.RKeyWord;            // reserved key word (e.g. if, else etc.)
        }
        else if (lexemeCurrent.IsSyntacticName)
        {
            if (lexemeNext.Text == "(" && lexemeNextOnSameLine)
            {
                TokenType = TokenTypes.RFunctionName;       
            }
            else
            {
                TokenType = TokenTypes.RSyntacticName;      
            }
        }
        else if (lexemeCurrent.IsComment)
        {
            TokenType = TokenTypes.RComment;            // comment (starts with '#*')
        }
        else if (lexemeCurrent.IsConstantString)
        {
            TokenType = TokenTypes.RConstantString;
        }
        else if (lexemeCurrent.IsNewLine)
        {
            TokenType = TokenTypes.RNewLine;
        }
        else if (lexemeCurrent.Text == ";")
        {
            TokenType = TokenTypes.REndStatement;
        }
        else if (lexemeCurrent.Text == ",")
        {
            TokenType = TokenTypes.RSeparator;          // parameter separator
        }
        else if (lexemeCurrent.IsSequenceOfSpaces)
        {     
            // check for spaces needs to be after separator check,
            // else linefeed is recognised as space
            TokenType = TokenTypes.RSpace;
        }
        else if (lexemeCurrent.IsBracket)
        {
                TokenType = TokenTypes.RBracket;
        }
        else if (lexemeCurrent.IsOperatorBrackets)
        {
            TokenType = TokenTypes.ROperatorBracket;    // bracket operator (e.g. '[')
        }
        else if (lexemeCurrent.IsOperatorUnary &&
                   (string.IsNullOrEmpty(lexemePrev.Text) ||
                    !lexemePrev.IsOperatorBinaryParameterLeft ||
                    !lexemePrevOnSameLine))
        {
            TokenType = TokenTypes.ROperatorUnaryRight; // unary right operator (e.g. '!x')
        }
        else if (lexemeCurrent.Text == "~" &&
                 lexemePrev.IsOperatorBinaryParameterLeft &&
                 (!lexemeNext.IsOperatorBinaryParameterRight || !lexemeNextOnSameLine))
        {
            TokenType = TokenTypes.ROperatorUnaryLeft;  // unary left operator (e.g. x~)
        }
        else if (lexemeCurrent.IsOperatorReserved || lexemeCurrent.IsOperatorUserDefinedComplete)
        {
            TokenType = TokenTypes.ROperatorBinary;     // binary operator (e.g. '+')
        }
        else
        {
            throw new Exception("Lexeme has no valid token type.");
        }
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Creates and returns a clone of this object. </summary>
    /// <returns>   A clone of this object. </returns>
    /// --------------------------------------------------------------------------------------------
    public RToken CloneMe()
    {
        var token = new RToken(Lexeme, ScriptPos, TokenType);
        foreach (RToken child in ChildTokens)
        {
            token.ChildTokens.Add(child.CloneMe());
        }
        return token;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Sets the token type to <see cref="TokenTypes.REndStatement"/>.
    /// This function is needed because the constructor cannot always correctly identify whether 
    /// a newline is an end statement or just for readability. During later parsing, the correct 
    /// token type can be identified.
    /// </summary>
    /// --------------------------------------------------------------------------------------------
    public void SetAsEndStatement()
    {
        TokenType = TokenTypes.REndStatement;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Sets the token type to <see cref="TokenTypes.RNewLine"/>.
    /// This function is needed because the constructor cannot always correctly identify whether 
    /// a newline is an end statement or just for readability. During later parsing, the correct 
    /// token type can be identified.
    /// </summary>
    /// --------------------------------------------------------------------------------------------
    public void SetAsNewLine()
    {
        TokenType = TokenTypes.RNewLine;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Returns true if token is not part of the functional R script, and its sole purpose is to 
    /// improve the presentation of the script for human readers. 
    /// Presentation tokens include comments, spaces, tabs and extra newlines (newlines added for 
    /// readability rather than to indicate the end of a statement).
    /// </summary>
    /// <returns> True if the token is for human readability (e.g. space, comments etc.), rather
    ///           than functionality.</returns>
    /// --------------------------------------------------------------------------------------------
    private bool GetIsPresentation()
    {
        switch (TokenType)
        {
            case TokenTypes.RPresentation:
            case TokenTypes.RSpace:
            case TokenTypes.RComment:
            case TokenTypes.RNewLine:
                {
                    return true;
                }
        }
        return false;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Recursively searches the token tree (i.e. this token and its children) for the token with 
    /// the latest end position in the script. If this token represents an R statement, then this
    /// will be the end position of the statement.
    /// </summary>
    /// <returns>The latest end position in the script of this token or its children.</returns>
    /// --------------------------------------------------------------------------------------------
    private uint GetPosEndStatement()
    {
        uint posEndStatement = ScriptPos + (uint)Lexeme.Text.Length;
        foreach (RToken token in ChildTokens)
        {
            posEndStatement = Math.Max(posEndStatement, token.GetPosEndStatement());
        }
        return posEndStatement;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Recursively searches the token tree (i.e. this token and its children) for the token with 
    /// the earliest start position in the script. If this token represents an R statement, then this
    /// will be the start position of the statement.
    /// </summary>
    /// <returns>The earliest start position in the script of this token or its children.</returns>
    /// --------------------------------------------------------------------------------------------
    private uint GetPosStartStatement()
    {
        uint posStartStatement = ScriptPos;
        foreach (RToken token in ChildTokens)
        {
            posStartStatement = Math.Min(posStartStatement, token.GetPosStartStatement());
        }
        return posStartStatement;
    }

}