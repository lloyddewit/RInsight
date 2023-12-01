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

        var lexemes = new RLexemeList(script).Lexemes;
        if (lexemes.Count == 0)
        {
            return;
        }

        var lexemePrev = new RLexeme("");
        var lexemeCurrent = new RLexeme("");
        var lexemeNext = new RLexeme("");
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
            statementContainsElement = statementContainsElement ? true : lexemeCurrent.IsElement;

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
            token = new RToken(lexemePrev, lexemeCurrent, lexemeNext, lexemePrevOnSameLine, lexemeNextOnSameLine, scriptPos);
            scriptPos += (uint)lexemeCurrent.Text.Length;

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
                            if (!(token.tokentype == RToken.TokenType.RComment || token.tokentype == RToken.TokenType.RPresentation || token.tokentype == RToken.TokenType.RSpace || token.tokentype == RToken.TokenType.RNewLine))
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
                            if (token.tokentype == RToken.TokenType.RNewLine && statementContainsElement && numOpenBrackets.Peek() == 0 && !lexemePrev.IsOperatorUserDefined && !(lexemePrev.IsOperatorReserved && !(lexemePrev.Text == "~")))
                            {                  // if statement contains at least one R element (i.e. not just spaces, comments, or newlines)
                                               // if there are no open brackets
                                               // if line doesn't end in a user-defined operator
                                               // if line doesn't end in a predefined operator
                                               // unless it's a tilda (the only operator that doesn't need a right-hand value) {
                                token.tokentype = RToken.TokenType.REndStatement;
                                statementContainsElement = false;
                            }

                            if (token.tokentype == RToken.TokenType.REndStatement && isScriptEnclosedByCurlyBrackets.Peek() == false && string.IsNullOrEmpty(lexemeNext.Text))
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
            if (token.tokentype == RToken.TokenType.REndScript && string.IsNullOrEmpty(lexemeNext.Text))
            {
                for (int nextPos = pos + 1; nextPos <= lexemes.Count - 1; nextPos++)
                {
                    lexemeCurrent = lexemes[nextPos];

                    token = new RToken(new RLexeme(""), lexemeCurrent, new RLexeme(""), false, false, scriptPos);
                    scriptPos += (uint)lexemeCurrent.Text.Length;

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
}