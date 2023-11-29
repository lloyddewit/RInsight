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
    public List<RToken> Tokens { get; }

    /// <summary>
    /// A token is a string of characters that represent a valid R element, plus meta 
    ///             data about the token type (identifier, operator, keyword, bracket etc.). 
    /// </summary>
    /// <param name="script"></param>
    /// <exception cref="Exception"></exception>
    public RTokenList(string script) 
    {
        Tokens = new List<RToken>();
        if (string.IsNullOrEmpty(script))
        {
            return;
        }

        List<string> lexemes = GetLstLexemes(script);
        if (lexemes.Count == 0)
        {
            return;
        }

        string lexemePrev = "";
        string lexemeCurrent = "";
        string lexemeNext;
        bool lexemePrevOnSameLine = false;
        bool lexemeNextOnSameLine;
        bool statementContainsElement = false;
        RToken token;

        var numOpenBrackets = new Stack<int>();
        numOpenBrackets.Push(0);

        var isScriptEnclosedByCurlyBrackets = new Stack<bool>();
        isScriptEnclosedByCurlyBrackets.Push(true);

        var tokenState = new Stack<tokenState>();
        tokenState.Push(RTokenList.tokenState.WaitingForStartScript);

        uint scriptPos = 0U;

        for (int pos = 0, loopTo = lexemes.Count - 1; pos <= loopTo; pos++)
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
            if (RToken.IsElement(lexemeCurrent))
            {
                lexemePrev = lexemeCurrent;
                lexemePrevOnSameLine = true;
            }
            else if (RToken.IsNewLine(lexemeCurrent))
            {
                lexemePrevOnSameLine = false;
            }

            lexemeCurrent = lexemes[pos];
            statementContainsElement = statementContainsElement ? true : RToken.IsElement(lexemeCurrent);

            // find next lexeme that represents an R element
            lexemeNext = "";
            lexemeNextOnSameLine = true;
            for (int nextPos = pos + 1; nextPos <= lexemes.Count - 1; nextPos++)
            {
                string lexeme = lexemes[nextPos];
                if (RToken.IsNewLine(lexeme))
                {
                    lexemeNextOnSameLine = false;
                }
                else if (RToken.IsElement(lexeme))
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
            switch (lexemeCurrent)
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
            token = new RToken(lexemePrev, lexemeCurrent, lexemeNext, lexemePrevOnSameLine, lexemeNextOnSameLine, scriptPos);
            scriptPos += (uint)lexemeCurrent.Length;

            // Process key words
            // Determine whether the next end statement will also be the end of the current script.
            // Normally, a '}' indicates the end of the current script. However, R allows single
            // statement scripts, not enclosed with '{}' for selected key words. 
            // The key words that allow this are: if, else, while, for and function.
            // For example:
            // if(x <= 0) y <- log(1+x) else y <- log(x)
            if (token.tokentype == RToken.TokenType.RComment || token.tokentype == RToken.TokenType.RSpace)
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
                            if (!(token.tokentype == RToken.TokenType.RNewLine))
                            {
                                if (token.text == "(")
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
                            if (!(token.tokentype == RToken.TokenType.RComment || token.tokentype == RToken.TokenType.RPresentation || token.tokentype == RToken.TokenType.RSpace || token.tokentype == RToken.TokenType.RNewLine))
                            {
                                tokenState.Pop();
                                tokenState.Push(RTokenList.tokenState.WaitingForEndScript);
                                if (token.text == "{")
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
                            if (token.tokentype == RToken.TokenType.RNewLine && statementContainsElement && numOpenBrackets.Peek() == 0 && !RToken.IsOperatorUserDefined(lexemePrev) && !(RToken.IsOperatorReserved(lexemePrev) && !(lexemePrev == "~")))
                            {                  // if statement contains at least one R element (i.e. not just spaces, comments, or newlines)
                                               // if there are no open brackets
                                               // if line doesn't end in a user-defined operator
                                               // if line doesn't end in a predefined operator
                                               // unless it's a tilda (the only operator that doesn't need a right-hand value) {
                                token.tokentype = RToken.TokenType.REndStatement;
                                statementContainsElement = false;
                            }

                            if (token.tokentype == RToken.TokenType.REndStatement && isScriptEnclosedByCurlyBrackets.Peek() == false && string.IsNullOrEmpty(lexemeNext))
                            {
                                token.tokentype = RToken.TokenType.REndScript;
                            }

                            if (token.tokentype == RToken.TokenType.REndScript)
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
            Tokens.Add(token);

            // Edge case: if the script has ended and there are no more R elements to process, 
            // then ensure that only formatting lexemes (i.e. spaces, newlines or comments) follow
            // the script's final statement.
            if (token.tokentype == RToken.TokenType.REndScript && string.IsNullOrEmpty(lexemeNext))
            {
                for (int nextPos = pos + 1; nextPos <= lexemes.Count - 1; nextPos++)
                {
                    lexemeCurrent = lexemes[nextPos];

                    token = new RToken("", lexemeCurrent, "", false, false, scriptPos);
                    scriptPos += (uint)lexemeCurrent.Length;

                    switch (token.tokentype)
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
                    Tokens.Add(token);
                }
                return;
            }
        }
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