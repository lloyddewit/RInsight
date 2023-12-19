namespace RInsight;

/// --------------------------------------------------------------------------------------------
/// <summary>
/// A list of tokens generated from an R script. Each item in the list is a recursive token tree 
/// that represents a single R statement. Each R statement may contain zero or more substatements.
/// The list of tokens is a lossless representation of the R script. It contains all the 
/// information needed to reconstruct the original script including all the whitespace, comments 
/// and extra line breaks.
/// For more details about R tokens and how they are structured, please see the documentation for 
/// the RToken class.
/// </summary>
/// --------------------------------------------------------------------------------------------
public class RTokenList {

    /// <summary> List of tokens that represents the R script </summary>
    public List<RToken> Tokens { get; private set; }

    // Indexes to the _operatorPrecedences array for operators with special characteristics
    private static readonly int _operatorsUnaryOnly = 4;
    private static readonly int _operatorsUserDefined = 6;
    private static readonly int _operatorsTilda = 14;

    /// <summary>   The relative precedence of the R operators. This is a two-dimensional array 
    ///             because the operators are stored in groups together with operators that 
    ///             have the same precedence.</summary>
    private static readonly string[][] _operatorPrecedences = new string[][]
        {
            new string[] { "::", ":::" },
            new string[] { "$", "@" },
            new string[] { "[", "[[" }, // bracket operators
            new string[] { "^" },       // right to left precedence
            new string[] { "-", "+" },  // unary operarors
            new string[] { ":" },
            new string[] { "%" },       // any operator that starts with '%' (including user-defined operators)
            new string[] { "|>" },
            new string[] { "*", "/" },
            new string[] { "+", "-" },
            new string[] { "<", ">", "<>", "<=", ">=", "==", "!=" },
            new string[] { "!" },
            new string[] { "&", "&&" },
            new string[] { "|", "||" },
            new string[] { "~" },       // unary or binary
            new string[] { "->", "->>" },
            new string[] { "<-", "<<-" },
            new string[] { "=" },
            new string[] { "?", "??" }
        };

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Constructs a list of tokens generated from <paramref name="script"/>. Each item in the list 
    /// is a recursive token tree that represents a single R statement. Each R statement may contain 
    /// zero or more substatements.
    /// The list of tokens is a lossless representation of the R script. It contains all the 
    /// information needed to reconstruct the original script including all the whitespace, comments 
    /// and extra line breaks.
    /// For more details about R tokens and how they are structured, please see the documentation for 
    /// the RToken class.
    /// </summary>
    /// <param name="script"> The R script to parse. This must be valid R according to the 
    /// R language specification at https://cran.r-project.org/doc/manuals/r-release/R-lang.html 
    /// (referenced 01 Feb 2021).</param>
    /// --------------------------------------------------------------------------------------------
    public RTokenList(string script) 
    {        
        Tokens = GetTokenTreeList(GetTokenList(script));
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns a clone of the next token in the <paramref name="tokens"/> list, 
    ///             after <paramref name="posTokens"/>. If there is no next token then throws 
    ///             an exception.</summary>
    /// 
    /// <param name="tokens">    The list of tokens. </param>
    /// <param name="posTokens"> The position of the current token in the list. </param>
    /// 
    /// <returns>   A clone of the next token in the <paramref name="tokens"/> list. </returns>
    /// --------------------------------------------------------------------------------------------
    private static RToken GetNextToken(List<RToken> tokens, int posTokens)
    {
        if (posTokens >= tokens.Count - 1)
        {
            throw new Exception("Token list ended unexpectedly.");
        }
        return tokens[posTokens + 1].CloneMe();
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Returns <paramref name="script"/> as a one-dimensional list of tokens. Each item in the list 
    /// represents a single R lexeme. 
    /// For more details about R lexemes, please see the documentation for the RLexeme class.
    /// </summary>
    /// <param name="script">  The R script to parse. Must be a valid R script.</param>
    /// <returns>              <paramref name="script"/> as a one-dimensional list of tokens. Each 
    ///                        item in the list represents a single R lexeme.</returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenList(string script)
    {
        var lexemes = new RLexemeList(script).Lexemes;
        if (lexemes.Count == 0)
        {
            return new List<RToken>();
        }

        var openBrackets = new HashSet<string> { "{", "(", "[", "[[" };
        var closeBrackets = new HashSet<string> { "}", ")", "]", "]]" };

        RLexeme lexemePrev = new RLexeme("");
        RLexeme lexemeCurrent = new RLexeme("");
        RLexeme lexemeNext;
        bool lexemePrevOnSameLine = false;
        bool lexemeNextOnSameLine;
        bool statementContainsElement = false;
        RToken token;

        int numOpenBrackets = 0;
        var tokenList = new List<RToken>();
        uint scriptPos = 0U;
        for (int pos = 0; pos < lexemes.Count; pos++)
        {
            // store previous non-space lexeme
            if (lexemeCurrent.IsElement)
            {
                lexemePrev = lexemeCurrent;
                lexemePrevOnSameLine = true;
            }
            else if (lexemeCurrent.IsNewLine)
            {
                lexemePrevOnSameLine = false;
            }

            lexemeCurrent = lexemes[pos];
            statementContainsElement = statementContainsElement || lexemeCurrent.IsElement;

            // find next lexeme that represents an R element
            lexemeNext = new RLexeme("");
            lexemeNextOnSameLine = true;
            for (int nextPos = pos + 1; nextPos <= lexemes.Count - 1; nextPos++)
            {
                RLexeme lexeme = lexemes[nextPos];
                if (lexeme.IsNewLine)
                {
                    lexemeNextOnSameLine = false;
                }
                else if (lexeme.IsElement)
                {
                    lexemeNext = lexeme;
                    break;
                }
            }

            numOpenBrackets += openBrackets.Contains(lexemeCurrent.Text) ? 1 : 0;
            numOpenBrackets -= closeBrackets.Contains(lexemeCurrent.Text) ? 1 : 0;

            // identify the token associated with the current lexeme and add the token to the list
            bool statementHasOpenBrackets = numOpenBrackets > 0;
            token = new RToken(lexemePrev, lexemeCurrent, lexemeNext,
                               lexemePrevOnSameLine, lexemeNextOnSameLine, scriptPos,
                               statementHasOpenBrackets, statementContainsElement);
            scriptPos += (uint)lexemeCurrent.Text.Length;
            if (token.TokenType == RToken.TokenTypes.REndStatement)
            {
                statementContainsElement = false;
            }

            // add new token to token list
            tokenList.Add(token);
        }
        return tokenList;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="tokens"/>. If the token is aan open bracket, 
    /// then makes everything inside the brackets a child of the open bracket token. Brackets may 
    /// be nested. 
    /// For example, '(a*(b+c))' is structured as:<code>
    ///   (<para>
    ///   ..a</para><para>
    ///   ..*</para><para>
    ///   ..(</para><para>
    ///   ....b</para><para>
    ///   ....+</para><para>
    ///   ....c</para><para>
    ///   ....)</para><para>
    ///   ..)</para></code></summary>
    /// 
    /// <param name="tokens">  The token tree to restructure. </param>
    /// <returns>              A token tree restructured for brackets. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeBrackets(List<RToken> tokens)
    {
        var openBrackets = new HashSet<string> { "{", "(", "[", "[[" };
        var closeBrackets = new HashSet<string> { "}", ")", "]", "]]" };

        var tokensNew = new List<RToken>();
        int pos = 0;
        while (pos < tokens.Count)
        {
            RToken token = tokens[pos];
            pos++;
            if (openBrackets.Contains(token.Lexeme.Text))
            {
                int numOpenBrackets = 1;
                while (pos < tokens.Count)
                {
                    RToken tokenTmp = tokens[pos];
                    pos++;

                    numOpenBrackets += openBrackets.Contains(tokenTmp.Lexeme.Text) ? 1 : 0;
                    numOpenBrackets -= closeBrackets.Contains(tokenTmp.Lexeme.Text) ? 1 : 0;

                    if (numOpenBrackets == 0)
                    {
                        token.ChildTokens = GetTokenTreeBrackets(token.CloneMe().ChildTokens);
                        token.ChildTokens.Add(tokenTmp.CloneMe());
                        break;
                    }
                    token.ChildTokens.Add(tokenTmp.CloneMe());
                }
            }
            tokensNew.Add(token.CloneMe());
        }
        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the <paramref name="tokens"/> tree. If the token is a ',' then it makes everything 
    /// up to the next ',' or close bracket a child of the ',' token. 
    /// In R, commas are allowed as separators for function parameters and square bracket parameters. 
    /// For example 'f1(a, b)', `a[b,c]`, `a[b,]` etc.
    /// Parameters between commas are optional. For example, 
    /// `myFunction(a,,b)` is structured as: <code>
    ///   myFunction<para>
    ///   ..(</para><para>
    ///   ....a</para><para>
    ///   ....,</para><para>
    ///   ....,</para><para>
    ///   ......b</para><para>
    ///   ....)</para></code>
    /// </summary>
    /// <param name="tokens">  The token tree to restructure. </param>
    /// <returns>              A token tree restructured for comma separators. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeCommas(List<RToken> tokens)
    {
        var tokensNew = new List<RToken>();

        int posTokens = 0;
        while (posTokens < tokens.Count)
        {
            RToken token = tokens[posTokens];
            posTokens++;

            if (token.TokenType == RToken.TokenTypes.RSeparator)
            {
                // -1 because we don't want to process the last token which is the close bracket
                while (posTokens < tokens.Count-1)
                {
                    RToken tokenTmp = tokens[posTokens];
                    // make each token up to next separator or close bracket, a child of the comma
                    if (tokenTmp.TokenType == RToken.TokenTypes.RSeparator)
                    {
                        break;
                    }
                    token.ChildTokens.Add(tokenTmp.CloneMe());
                    posTokens++;
                }
            }

            token.ChildTokens = GetTokenTreeCommas(token.CloneMe().ChildTokens);
            tokensNew.Add(token.CloneMe());
        }

        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the <paramref name="tokens"/> tree. If the token is a function name then it makes 
    /// the subsequent '(' a child of the function name token. </summary>
    /// 
    /// <param name="tokens">  The token tree to restructure. </param>
    /// <returns>              A token tree restructured for functions. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeFunctions(List<RToken> tokens)
    {
        var tokensNew = new List<RToken>();
        int pos = 0;
        while (pos < tokens.Count)
        {
            RToken token = tokens[pos];
            if (token.TokenType == RToken.TokenTypes.RFunctionName)
            {
                // if next steps will go out of bounds, then throw developer error
                if (pos > tokens.Count - 2)
                {
                    throw new Exception(
                        "The function's parameters have an unexpected format and cannot be processed.");
                }
                // make the function's open bracket a child of the function name
                pos ++;
                token.ChildTokens.Add(tokens[pos].CloneMe());
            }
            token.ChildTokens = GetTokenTreeFunctions(token.CloneMe().ChildTokens);
            tokensNew.Add(token.CloneMe());
            pos ++;
        }
        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Constructs a list of token trees generated from <paramref name="tokenList"/>. 
    /// Each item in the list is a recursive token tree that represents a single R statement. 
    /// Each R statement may contain zero or more substatements.
    /// </summary>
    /// <param name="tokenList">  A one-dimensional list of tokens representing an R script.</param>
    /// <returns>                 A list of token trees generated from <paramref name="tokenList"/>.
    ///                           </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeList(List<RToken> tokenList)
    {
        var tokenTreeList = new List<RToken>();
        int pos = 0;
        while (pos < tokenList.Count)
        {
            // create list of tokens for this statement
            var statementTokens = new List<RToken>();
            while (pos < tokenList.Count)
            {
                statementTokens.Add(tokenList[pos]);
                pos++;
                // we don't add this termination condition to the while statement
                //   because we also want the token that terminates the statement.
                if (tokenList[pos - 1].TokenType == RToken.TokenTypes.REndStatement)
                {
                    break;
                }
            }

            // restructure the statement's token list into a token tree
            var tokenTreePresentation = GetTokenTreePresentation(statementTokens);
            var tokenTreeBrackets = GetTokenTreeBrackets(tokenTreePresentation);
            var tokenTreeCommas = GetTokenTreeCommas(tokenTreeBrackets);
            var tokenTreeFunctions = GetTokenTreeFunctions(tokenTreeCommas);
            var tokenTreeOperators = GetTokenTreeOperators(tokenTreeFunctions);

            if (tokenTreeOperators.Count == 0
                || (tokenTreeOperators.Count == 1 && pos < tokenList.Count)
                || tokenTreeOperators.Count > 2)
            {
                throw new Exception("The token tree for a statement must contain a single token "
                        + "followed by an endStatement token.\n" 
                        + "Special case: for the last statement in the script, an endStatement "
                        + "token is optional.");
            }
            tokenTreeList.Add(tokenTreeOperators[0].CloneMe());
            if (tokenTreeOperators.Count > 1)
            {
                tokenTreeList.Add(tokenTreeOperators[1].CloneMe());
            }
        }
        return tokenTreeList;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the tree of tokens in <paramref name="tokens"/>. If one of the operators in 
    /// the <paramref name="posOperators"/> group is found, then the operator's parameters 
    /// (typically the tokens to the left and right of the operator) are made children of the 
    /// operator. For example, 'a*b+c' is structured as:<code>
    ///   +<para>
    ///   ..*</para><para>
    ///   ....a</para><para>
    ///   ....b</para><para>
    ///   ..c</para></code>
    /// 
    /// Edge case: This function cannot process the  case where a binary operator is immediately 
    /// followed by a unary operator with the same or a lower precedence (e.g. 'a^-b', 'a+~b', 
    /// 'a~~b' etc.). This is because of the R default precedence rules. The workaround is to 
    /// enclose the unary operator in brackets (e.g. 'a^(-b)', 'a+(~b)', 'a~(~b)' etc.).
    /// </summary>
    /// <param name="tokens">        The token tree to restructure. </param>
    /// <param name="posOperators">  The group of operators to search for in the tree. </param>
    /// 
    /// <returns>   A token tree restructured for the specified group of operators. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeOperatorGroup(List<RToken> tokens, int posOperators)
    {
        if (tokens.Count < 1)
        {
            return new List<RToken>();
        }

        var tokensNew = new List<RToken>();
        RToken? tokenPrev = null;
        bool prevTokenProcessed = false;

        int posTokens = 0;
        while (posTokens < tokens.Count)
        {
            RToken token = tokens[posTokens].CloneMe();

            // if the token is the operator we are looking for and it has not been processed already.
            // Edge case: if the operator already has (non-presentation) children then it means 
            // that it has already been processed. This happens when the child is in the 
            // same precedence group as the parent but was processed first in accordance 
            // with the left to right rule (e.g. 'a/b*c').
            if ((_operatorPrecedences[posOperators].Contains(token.Lexeme.Text)
                 || posOperators == _operatorsUserDefined 
                 && token.Lexeme.IsOperatorUserDefinedComplete)
                && (token.ChildTokens.Count == 0
                    || token.TokenType == RToken.TokenTypes.ROperatorBracket
                    || (token.ChildTokens.Count == 1
                        && token.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation)))
            {
                switch (token.TokenType)
                {
                    case RToken.TokenTypes.ROperatorBracket: // handles '[' and '[['
                        {
                            token.ChildTokens = GetOperatorBracketChildren(
                                    token.CloneMe().ChildTokens, tokenPrev);
                            prevTokenProcessed = true;
                            break;
                        }

                    case RToken.TokenTypes.ROperatorBinary:
                        {
                            // edge case: if we are looking for unary '+' or '-' and we found
                            //   a binary '+' or '-'
                            if (posOperators == _operatorsUnaryOnly)
                            {
                                // do not process
                                // (binary '+' and '-' have a lower precedence and will be processed later)
                                break;
                            }
                            token.ChildTokens.AddRange(GetOperatorBinaryChildren(tokens, 
                                    ref posTokens, tokenPrev));
                            prevTokenProcessed = true;
                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryRight:
                        {
                            // edge case: if we found a unary '+' or '-', but we are not currently
                            //            processing the unary '+'and '-' operators
                            if (_operatorPrecedences[_operatorsUnaryOnly].Contains(token.Lexeme.Text) 
                                && !(posOperators == _operatorsUnaryOnly))
                            {
                                break;
                            }
                            // make the next token, the child of the current operator token
                            token.ChildTokens.Add(GetNextToken(tokens, posTokens));
                            posTokens++;
                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryLeft:
                        {
                            if (tokenPrev == null || !(posOperators == _operatorsTilda))
                            {
                                throw new Exception("Illegal unary left operator ('~' is the "
                                                    + "only valid unary left operator).");
                            }
                            // make the previous token, the child of the current operator token
                            token.ChildTokens.Add(tokenPrev.CloneMe());
                            prevTokenProcessed = true;
                            break;
                        }
                    default:
                        {
                            throw new Exception("The token has an unknown operator type.");
                        }
                }
            }

            // if token was not the operator we were looking for
            // (or we were looking for a unary right operator)
            if (!prevTokenProcessed && tokenPrev != null)
            {
                // add the previous token to the tree
                tokensNew.Add(tokenPrev);
            }

            // process the current token's children
            token.ChildTokens = GetTokenTreeOperatorGroup(token.CloneMe().ChildTokens, posOperators);

            tokenPrev = token.CloneMe();
            prevTokenProcessed = false;
            posTokens++;
        }

        if (tokenPrev == null)
        {
            throw new Exception("Expected that there would still be a token to add to the tree.");
        }
        tokensNew.Add(tokenPrev.CloneMe());

        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary> 
    /// Iterates through all the possible operators in order of precedence. For each operator, 
    /// traverses the tree of tokens in <paramref name="tokens"/>. If the operator is found then 
    /// the operator's parameters (typically the tokens to the left and right of the operator) are 
    /// made children of the operator. For example, 'a*b+c' is structured as:<code>
    ///   +<para>
    ///   ..*</para><para>
    ///   ....a</para><para>
    ///   ....b</para><para>
    ///   ..c</para></code></summary>
    /// 
    /// <param name="tokens">  The token tree to restructure. </param>
    /// <returns>              A token tree restructured for all the possible operators. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeOperators(List<RToken> tokens)
    {
        var tokensNew = new List<RToken>();
        if (tokens.Count <= 0)
        {
            return tokensNew;
        }

        for (int posOperators = 0; posOperators < _operatorPrecedences.Length; posOperators++)
        {
            // restructure the tree for the next group of operators in the precedence list
            tokensNew = GetTokenTreeOperatorGroup(tokens, posOperators);

            // clone the new tree before restructuring for the next operator
            tokens = new List<RToken>();
            foreach (RToken tokenTmp in tokensNew)
            {
                tokens.Add(tokenTmp.CloneMe());
            }
        }
        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="tokens"/> and makes each presentation element 
    /// a child of the next non-presentation element. 
    /// <para>
    /// A presentation element is an element that has no functionality and is only used to make 
    /// the script easier to read. It may be a block of spaces, a comment or a newline that does
    /// not end a statement.
    /// </para><para>
    /// For example, the list of tokens representing the following block of script:
    /// </para><code>
    /// # comment1\n <para>
    /// a =b # comment2 </para></code><para>
    /// </para><para>
    /// Will be structured as:</para><code><para>
    /// a</para><para>
    /// .."# comment1\n"</para><para>
    /// =</para><para>
    /// .." "</para><para>
    /// b</para><para>
    /// (endStatement)</para><para>
    /// .." # comment2"</para><para>
    /// </para></code></summary>
    /// <param name="tokens">  The token tree to restructure. </param>
    /// <returns>              A token tree restructured for presentation information. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreePresentation(List<RToken> tokens)
    {

        if (tokens.Count < 1)
        {
            return new List<RToken>();
        }

        var tokensNew = new List<RToken>();
        RToken token;
        string prefix = "";
        uint prefixScriptPos = 0;
        int pos = 0;
        while (pos < tokens.Count)
        {
            token = tokens[pos];
            pos ++;
            switch (token.TokenType)
            {
                case RToken.TokenTypes.RSpace:
                case RToken.TokenTypes.RComment:
                case RToken.TokenTypes.RNewLine:
                    {
                        if (string.IsNullOrEmpty(prefix))
                        {
                            prefixScriptPos = token.ScriptPosStartStatement;
                        }
                        prefix += token.Lexeme.Text;                        
                        break;
                    }

                default:
                    {
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            token.ChildTokens.Add(new RToken(new RLexeme(prefix), prefixScriptPos, RToken.TokenTypes.RPresentation));
                        }
                        tokensNew.Add(token.CloneMe());
                        prefix = "";
                        prefixScriptPos = 0;
                        break;
                    }
            }
        }

        // Edge case: if there is still presentation information not yet added to a tree element
        // (this may happen if the last statement in the script is not terminated 
        // with a new line or '}')
        if (!string.IsNullOrEmpty(prefix))
        {
            token = new RToken(new RLexeme(""), prefixScriptPos, RToken.TokenTypes.REmpty);
            tokensNew.Add(token);

            // add a new end statement token that contains the presentation information
            token = new RToken(new RLexeme(""), prefixScriptPos + (uint)prefix.Length, RToken.TokenTypes.REndStatement);
            token.ChildTokens.Add(new RToken(new RLexeme(prefix), prefixScriptPos, RToken.TokenTypes.RPresentation));
            tokensNew.Add(token);
        }

        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Processes the binary operator at position <paramref name="posTokens"/> in the 
    /// <paramref name="tokens"/> list.
    /// Each binary operator must have a left-hand operand (the token preceding the operator token 
    /// in the <paramref name="tokens"/> list); and one or more right-hand operands (the token(s) 
    /// following the operator token in the <paramref name="tokens"/> list).
    /// An example of multiple right-hand operands is 'a+b+c+d'. 'b', 'c' and 'd' are all right-hand 
    /// operands of the '+' operator.
    /// </summary>
    /// <param name="tokens"></param>
    /// <param name="posTokens">  </param>
    /// <param name="tokenPrev">  The token preceeding the operator token (should represent the 
    ///                           left-hand operand)</param>
    /// <returns>                 The <paramref name="tokens"/> list restructured for the binary 
    ///                           operator at position <paramref name="posTokens"/>.</returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetOperatorBinaryChildren(List<RToken> tokens, ref int posTokens, 
                                                          RToken? tokenPrev)
    {
        if (tokenPrev == null)
        {
            throw new Exception("The binary operator has no parameter on its left.");
        }

        List<RToken> childTokens = new List<RToken>();
        RToken.TokenTypes tokenType = tokens[posTokens].TokenType;
        string tokenText = tokens[posTokens].Lexeme.Text ?? "";

        // make the previous and next tokens, the children of the current token
        childTokens.Add(tokenPrev.CloneMe());
        childTokens.Add(GetNextToken(tokens, posTokens));
        posTokens++;
        // while next token is the same operator (e.g. 'a+b+c+d...'), 
        // then keep making the next token, the child of the current operator token
        while (posTokens < tokens.Count - 1)
        {
            RToken tokenNext = GetNextToken(tokens, posTokens);
            if (tokenType != tokenNext.TokenType || tokenText != tokenNext.Lexeme.Text)
            {
                break;
            }
            posTokens++;
            childTokens.Add(GetNextToken(tokens, posTokens));
            posTokens++;
        }
        return childTokens;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Adds the bracket operator's left-hand operand (<paramref name="tokenPrev"/>) to the bracket 
    /// operator's children (<paramref name="tokens"/>).
    /// For example, the left-hand operand in 'a[b]' is 'a'.
    /// The left-hand operand is made the first non-presentation child of the bracket operator.
    /// </summary>
    /// <param name="tokens">     The bracket operator's existing children.</param>
    /// <param name="tokenPrev">  The token representing the left-hand operand.</param>
    /// <returns>                 A restructured list of children for the bracket operator.</returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetOperatorBracketChildren(List<RToken> tokens, RToken? tokenPrev)
    {
        List<RToken> tokensNew = new List<RToken>();
        if (tokenPrev == null)
        {
            if (tokens.Count > 2
                && tokens[tokens.Count - 1].TokenType == RToken.TokenTypes.ROperatorBracket)
            {
                // this bracket operator has already been processed so no further action needed
                return tokens;
            }
            throw new Exception("The bracket operator has no parameter on its left.");
        }

        // if there is a presentation token, then make it the first token in the list
        int posFirstNonPresentationChild = tokens[0].TokenType == RToken.TokenTypes.RPresentation ? 1 : 0;
        if (posFirstNonPresentationChild == 1)
        {
            tokensNew.Add(tokens[0].CloneMe());
        }
        // make the left-hand operand (e.g. 'a' in 'a[b]') the next child
        tokensNew.Add(tokenPrev.CloneMe());

        // make the right-hand operand(s) the next child(ren)
        tokensNew.AddRange(tokens.GetRange(posFirstNonPresentationChild, tokens.Count - posFirstNonPresentationChild));

        return tokensNew;
    }

}