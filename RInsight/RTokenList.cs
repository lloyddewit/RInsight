using RInsight;
using System.Linq;

namespace RInsight;

/// <summary>
/// TODO
/// </summary>
public class RTokenList {

    /// <summary>   The current state of the token parsing. </summary>
    private enum tokenState {
        WaitingForOpenCondition,
        WaitingForCloseCondition,
        WaitingForStartScript,
        WaitingForEndScript
    }

    /// <summary>
    /// A list of tokens generated from an R script. Each token in the list represents a single top-level statement in the script. Each token contains information about the type of statement (e.g. assignment, function definition, if statement etc.) and the position of the statement in the script.
    /// </summary>
    public List<RToken> Tokens { get; private set; }

    // Constants for operator precedence groups that have special characteristics (e.g. must be unary)
    private static readonly int _operatorsBrackets = 2;
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
    /// A token is a string of characters that represent a valid R element, plus meta 
    /// data about the token type (identifier, operator, keyword, bracket etc.). 
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
    ///             an error.</summary>
    /// 
    /// <param name="tokens">      The list of tokens. </param>
    /// <param name="posTokens">     The position of the current token in the list. </param>
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
    /// todo refactor for key words
    /// </summary>
    /// <param name="script"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenList(string script)
    {
        var lexemes = new RLexemeList(script).Lexemes;
        if (lexemes.Count == 0)
        {
            return new List<RToken>();
        }

        RLexeme lexemePrev = new RLexeme("");
        RLexeme lexemeCurrent = new RLexeme("");
        RLexeme lexemeNext;
        bool lexemePrevOnSameLine = false;
        bool lexemeNextOnSameLine;
        bool statementContainsElement = false;
        RToken token;

        var numOpenBrackets = new Stack<int>();
        numOpenBrackets.Push(0);

        var isScriptEnclosedByCurlyBrackets = new Stack<bool>();
        isScriptEnclosedByCurlyBrackets.Push(true); // todo should we push true here?

        var tokenState = new Stack<tokenState>();
        tokenState.Push(RTokenList.tokenState.WaitingForStartScript);

        var tokenList = new List<RToken>();
        uint scriptPos = 0U;
        for (int pos = 0; pos < lexemes.Count; pos++)
        {
            if (numOpenBrackets.Count < 1)
            {
                throw new Exception("The stack storing the number of open brackets must have at least one value.");
            }
            else if (isScriptEnclosedByCurlyBrackets.Count < 1)
            {
                throw new Exception("The stack storing the number of open curly brackets must have at least one value.");
            }
            else if (tokenState.Count < 1)
            {
                throw new Exception("The stack storing the current state of the token parsing must have at least one value.");
            }

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

            // determine whether the current sequence of tokens makes a complete valid R statement
            // This is needed to determine whether a newline marks the end of the statement
            // or is just for presentation.
            // The current sequence of tokens is considered a complete valid R statement if it 
            // has no open brackets and it does not end in an operator.
            switch (lexemeCurrent.Text)
            {
                case "(":
                case "[":
                case "[[":
                    {
                        numOpenBrackets.Push(numOpenBrackets.Pop() + 1);
                        break;
                    }
                case ")":
                case "]":
                case "]]":
                    {
                        numOpenBrackets.Push(numOpenBrackets.Pop() - 1);
                        break;
                    }
                case "if":
                case "while":
                case "for":
                case "function":
                    {
                        tokenState.Push(RTokenList.tokenState.WaitingForOpenCondition);
                        numOpenBrackets.Push(0);
                        break;
                    }
                case "else":
                case "repeat":
                    {
                        tokenState.Push(RTokenList.tokenState.WaitingForCloseCondition); // 'else' and 'repeat' keywords have no condition (e.g. 'if (x==1) y<-0 else y<-1'
                        numOpenBrackets.Push(0);                                 // after the keyword is processed, the state will automatically change to 'WaitingForEndScript'
                        break;
                    }
            }

            // identify the token associated with the current lexeme and add the token to the list
            bool statementHasOpenBrackets = numOpenBrackets.Peek() > 0;
            token = new RToken(lexemePrev, lexemeCurrent, lexemeNext, 
                               lexemePrevOnSameLine, lexemeNextOnSameLine, scriptPos,
                               statementHasOpenBrackets, statementContainsElement);
            scriptPos += (uint)lexemeCurrent.Text.Length;
            if (token.TokenType == RToken.TokenTypes.REndStatement)
            {
                statementContainsElement = false;
            }

            // Process key words
            // Determine whether the next end statement will also be the end of the current script.
            // Normally, a '}' indicates the end of the current script. However, R allows single
            // statement scripts, not enclosed with '{}' for selected key words. 
            // The key words that allow this are: if, else, while, for and function.
            // For example:
            // if(x <= 0) y <- log(1+x) else y <- log(x)
            if (token.TokenType == RToken.TokenTypes.RComment || token.TokenType == RToken.TokenTypes.RSpace)
            {       // ignore comments, spaces and newlines (they don't affect key word processing)
                    // clsToken.enuToken = clsRToken.typToken.RNewLine Then
                    // clsToken.enuToken = clsRToken.typToken.RKeyWord Then    'ignore keywords (already processed above)
                    // do nothing
            }
            else
            {
                switch (tokenState.Peek())
                {
                    case RTokenList.tokenState.WaitingForOpenCondition:
                        {
                            if (!(token.TokenType == RToken.TokenTypes.RNewLine))
                            {
                                if (token.Lexeme.Text == "(")
                                {
                                    tokenState.Pop();
                                    tokenState.Push(RTokenList.tokenState.WaitingForCloseCondition);
                                }
                            }
                            break;
                        }
                    case RTokenList.tokenState.WaitingForCloseCondition:
                        {
                            if (numOpenBrackets.Peek() == 0)
                            {
                                tokenState.Pop();
                                tokenState.Push(RTokenList.tokenState.WaitingForStartScript);
                            }
                            break;
                        }
                    case RTokenList.tokenState.WaitingForStartScript:
                        {
                            if (!(token.TokenType == RToken.TokenTypes.RComment 
                                  || token.TokenType == RToken.TokenTypes.RPresentation 
                                  || token.TokenType == RToken.TokenTypes.RSpace 
                                  || token.TokenType == RToken.TokenTypes.RNewLine))
                            {
                                tokenState.Pop();
                                tokenState.Push(RTokenList.tokenState.WaitingForEndScript);
                                if (token.Lexeme.Text == "{")
                                {
                                    isScriptEnclosedByCurlyBrackets.Push(true);  // script will terminate with '}'
                                }
                                else
                                {
                                    isScriptEnclosedByCurlyBrackets.Push(false);
                                } // script will terminate with end statement
                            }
                            break;
                        }
                    case RTokenList.tokenState.WaitingForEndScript:
                        {
                            if (token.TokenType == RToken.TokenTypes.RNewLine && statementContainsElement && numOpenBrackets.Peek() == 0 && !lexemePrev.IsOperatorUserDefined && !(lexemePrev.IsOperatorReserved && !(lexemePrev.Text == "~")))
                            {                  // if statement contains at least one R element (i.e. not just spaces, comments, or newlines)
                                               // if there are no open brackets
                                               // if line doesn't end in a user-defined operator
                                               // if line doesn't end in a predefined operator
                                               // unless it's a tilda (the only operator that doesn't need a right-hand value) {
                                               // TODO token.tokentype = RToken.TokenType.REndStatement;
                                statementContainsElement = false;
                            }

                            if (token.TokenType == RToken.TokenTypes.REndStatement && isScriptEnclosedByCurlyBrackets.Peek() == false && string.IsNullOrEmpty(lexemeNext.Text))
                            {
                                // TODO token.tokentype = RToken.TokenType.REndScript;
                            }

                            // todo if (token.Tokentype == RToken.TokenType.REndScript)
                            if (token.TokenType == RToken.TokenTypes.RNewLine
                                && statementContainsElement && numOpenBrackets.Peek() == 0
                                && !lexemePrev.IsOperatorUserDefined
                                && !(lexemePrev.IsOperatorReserved && !(lexemePrev.Text == "~"))
                                && isScriptEnclosedByCurlyBrackets.Peek() == false
                                && string.IsNullOrEmpty(lexemeNext.Text))
                            {
                                isScriptEnclosedByCurlyBrackets.Pop();
                                numOpenBrackets.Pop();
                                tokenState.Pop();
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
            tokenList.Add(token);

            // Edge case: if the script has ended and there are no more R elements to process, 
            // then ensure that only formatting lexemes (i.e. spaces, newlines or comments) follow
            // the script's final statement.
            //TODOif (token.Tokentype == RToken.TokenType.REndScript && string.IsNullOrEmpty(lexemeNext.Text))
            if (token.TokenType == RToken.TokenTypes.RNewLine
                && statementContainsElement && numOpenBrackets.Peek() == 0
                && !lexemePrev.IsOperatorUserDefined
                && !(lexemePrev.IsOperatorReserved && !(lexemePrev.Text == "~"))
                && isScriptEnclosedByCurlyBrackets.Peek() == false
                && string.IsNullOrEmpty(lexemeNext.Text))
            {
                for (int nextPos = pos + 1; nextPos <= lexemes.Count - 1; nextPos++)
                {
                    lexemeCurrent = lexemes[nextPos];

                    token = new RToken(new RLexeme(""), lexemeCurrent, new RLexeme(""), 
                                       false, false, scriptPos, false, false);
                    scriptPos += (uint)lexemeCurrent.Text.Length;

                    switch (token.TokenType)
                    {
                        case RToken.TokenTypes.RSpace:
                        case RToken.TokenTypes.RNewLine:
                        case RToken.TokenTypes.RComment:
                            {
                                break;
                            }
                        default:
                            {
                                throw new Exception("Only spaces, newlines and comments are " 
                                                    + "allowed after the script ends.");
                            }
                    }
                    // add new token to token list
                    tokenList.Add(token);
                }                
            }
        }
        return tokenList;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="tokens"/>. If the token is a '(' then makes 
    /// everything inside the brackets a child of the '(' token. Brackets may be nested. 
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
    /// <param name="tokens">   The token tree to restructure. </param>
    /// <returns>   A token tree restructured for round brackets. </returns>
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
    /// Traverses the tree of tokens in <paramref name="tokens"/>. If the token is a ',' that 
    /// separates function parameters, then it makes everything up to the next ',' or ')' a child 
    /// of the ',' token. Parameters between function commas are optional. For example, 
    /// `myFunction(a,,b)` is structured as: <code>
    ///   myFunction<para>
    ///   ..(</para><para>
    ///   ....a</para><para>
    ///   ....,</para><para>
    ///   ....,</para><para>
    ///   ......b</para><para>
    ///   ......)</para></code>
    /// Commas used within square brackets (e.g. `a[b,c]`, `a[b,]` etc.) are ignored.
    /// </summary>
    /// 
    /// <param name="tokens">        The token tree to restructure. </param>
    /// <param name="pos">             [in,out] The position in the list to start processing </param>
    /// <param name="processingComma"> (Optional) True if function called when already processing 
    ///     a comma (prevents commas being nested inside each other). </param>
    /// 
    /// <returns>   A token tree restructured for function commas. </returns>
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
                while (posTokens < tokens.Count-1) // -1 because we don't want to process the last token which is the close bracket
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
    /// Traverses the tree of tokens in <paramref name="tokens"/>. If the token is a function name then it 
    /// makes the subsequent '(' a child of the function name token. </summary>
    /// 
    /// <param name="tokens">   The token tree to restructure. </param>
    /// 
    /// <returns>   A token tree restructured for function names. </returns>
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
                    throw new Exception("The function's parameters have an unexpected format and cannot be processed.");
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
    /// todo
    /// </summary>
    /// <param name="tokenList"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
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

            // if the token is the operator we are looking for and it has not been processed already
            // Edge case: if the operator already has (non-presentation) children then it means 
            // that it has already been processed. This happens when the child is in the 
            // same precedence group as the parent but was processed first in accordance 
            // with the left to right rule (e.g. 'a/b*c').
            if ((_operatorPrecedences[posOperators].Contains(token.Lexeme.Text)
                 || posOperators == _operatorsUserDefined && token.Lexeme.IsOperatorUserDefinedComplete)
                && (token.ChildTokens.Count == 0
                    || token.TokenType == RToken.TokenTypes.ROperatorBracket
                    || (token.ChildTokens.Count == 1
                        && token.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation)))
            {
                switch (token.TokenType)
                {
                    case RToken.TokenTypes.ROperatorBracket: // handles '[' and '[['
                        {
                            token.ChildTokens = GetOperatorBracketChildren(token.CloneMe().ChildTokens, tokenPrev);
                            prevTokenProcessed = true;
                            break;
                        }

                    case RToken.TokenTypes.ROperatorBinary:
                        {
                            // edge case: if we are looking for unary '+' or '-' and we found a binary '+' or '-'
                            if (posOperators == _operatorsUnaryOnly)
                            {
                                // do not process (binary '+' and '-' have a lower precedence and will be processed later)
                                break;
                            }
                            else if (tokenPrev == null)
                            {
                                throw new Exception("The binary operator has no parameter on its left.");
                            }

                            // make the previous and next tokens, the children of the current token
                            token.ChildTokens.Add(tokenPrev.CloneMe());
                            prevTokenProcessed = true;
                            token.ChildTokens.Add(GetNextToken(tokens, posTokens));
                            posTokens++;
                            // while next token is the same operator (e.g. 'a+b+c+d...'), 
                            // then keep making the next token, the child of the current operator token
                            RToken tokenNext;
                            while (posTokens < tokens.Count - 1)
                            {
                                tokenNext = GetNextToken(tokens, posTokens);
                                if (!(token.TokenType == tokenNext.TokenType) || !((token.Lexeme.Text ?? "") == (tokenNext.Lexeme.Text ?? "")))
                                {
                                    break;
                                }

                                posTokens++;
                                token.ChildTokens.Add(GetNextToken(tokens, posTokens));
                                posTokens++;
                            }

                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryRight:
                        {
                            // edge case: if we found a unary '+' or '-', but we are not currently processing the unary '+'and '-' operators
                            if (_operatorPrecedences[_operatorsUnaryOnly].Contains(token.Lexeme.Text) && !(posOperators == _operatorsUnaryOnly))
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
                                throw new Exception("Illegal unary left operator ('~' is the only valid unary left operator).");
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
            if (!prevTokenProcessed && !(tokenPrev == null))
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
    /// <param name="tokens">   The token tree to restructure. </param>
    /// 
    /// <returns>   A token tree restructured for all the possible operators. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetTokenTreeOperators(List<RToken> tokens)
    {
        if (tokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var tokensNew = new List<RToken>();
        for (int posOperators = 0; posOperators < _operatorPrecedences.Length; posOperators++)
        {
            // restructure the tree for the next group of operators in the precedence list
            tokensNew = GetTokenTreeOperatorGroup(tokens, posOperators);

            // clone the new tree before restructuring for the next operator
            tokens = new List<RToken>();
            foreach (RToken tokenTmp in tokensNew)
                tokens.Add(tokenTmp.CloneMe());
        }
        return tokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="tokens"/>, from position 
    /// <paramref name="iPos"/> and makes each presentation element a child of the next 
    /// non-presentation element. 
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
    /// 
    /// <param name="tokens">   The list of tokens to process. </param>
    /// <param name="iPos">        The position in the list to start processing </param>
    /// 
    /// <returns>   A token tree where presentation information is stored as a child of the next 
    ///             non-presentation element. </returns>
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

    /// <summary>
    /// todo
    /// </summary>
    /// <param name="tokens"></param>
    /// <param name="tokenPrev"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
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

        // make the right-hand operand(s) the next children
        tokensNew.AddRange(tokens.GetRange(posFirstNonPresentationChild, tokens.Count - posFirstNonPresentationChild));

        return tokensNew;
    }

}