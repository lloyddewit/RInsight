using Microsoft.VisualBasic;
using System.Text.RegularExpressions;

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

    /// <summary>   The relative precedence of the R operators. This is a two-dimensional array 
    ///             because the operators are stored in groups together with operators that 
    ///             have the same precedence.</summary>
    private readonly string[][] operatorPrecedences = new string[20][];

    // Constants for operator precedence groups that have special characteristics (e.g. must be unary)
    private readonly int iOperatorsBrackets = 2;
    private readonly int iOperatorsUnaryOnly = 4;
    private readonly int iOperatorsUserDefined = 6;
    private readonly int iOperatorsTilda = 14;
    private readonly int iOperatorsRightAssignment = 15;
    private readonly int iOperatorsLeftAssignment1 = 16;
    private readonly int iOperatorsLeftAssignment2 = 17;


    /// <summary>
    /// A list of tokens generated from an R script. Each token in the list represents a single top-level statement in the script. Each token contains information about the type of statement (e.g. assignment, function definition, if statement etc.) and the position of the statement in the script.
    /// </summary>
    public List<RToken> Tokens { get; private set; }

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

        List<RToken> tokenList = GetTokenList(script);
        Tokens = GetTokenTreeList(tokenList);
    }

    private List<RToken> GetTokenList(string script)
    {
        var lexemes = new RLexemeList(script).Lexemes;
        if (lexemes.Count == 0)
        {
            return new List<RToken>();
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

        var tokenList = new List<RToken>();
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
            token = new RToken(lexemePrev, lexemeCurrent, lexemeNext, lexemePrevOnSameLine, lexemeNextOnSameLine, scriptPos, numOpenBrackets.Peek() > 0, statementContainsElement);
            scriptPos += (uint)lexemeCurrent.Text.Length;
            //todo
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
                            if (!(token.TokenType == RToken.TokenTypes.RComment || token.TokenType == RToken.TokenTypes.RPresentation || token.TokenType == RToken.TokenTypes.RSpace || token.TokenType == RToken.TokenTypes.RNewLine))
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

                    token = new RToken(new RLexeme(""), lexemeCurrent, new RLexeme(""), false, false, scriptPos, false, false);
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
                                throw new Exception("Only spaces, newlines and comments are allowed after the script ends.");
                            }
                    }
                    // add new token to token list
                    tokenList.Add(token);
                }                
            }
        }
        return tokenList;
    }

    private List<RToken> GetTokenTreeList(List<RToken> tokenList)
    {
        operatorPrecedences[0] = new string[] { "::", ":::" };
        operatorPrecedences[1] = new string[] { "$", "@" };
        operatorPrecedences[iOperatorsBrackets] = new string[] { "[", "[[" }; // bracket operators
        operatorPrecedences[3] = new string[] { "^" };                          // right to left precedence
        operatorPrecedences[iOperatorsUnaryOnly] = new string[] { "-", "+" }; // unary operarors
        operatorPrecedences[5] = new string[] { ":" };
        operatorPrecedences[iOperatorsUserDefined] = new string[] { "%" };    // any operator that starts with '%' (including user-defined operators)
        operatorPrecedences[7] = new string[] { "|>" };
        operatorPrecedences[8] = new string[] { "*", "/" };
        operatorPrecedences[9] = new string[] { "+", "-" };
        operatorPrecedences[10] = new string[] { "<", ">", "<>", "<=", ">=", "==", "!=" };
        operatorPrecedences[11] = new string[] { "!" };
        operatorPrecedences[12] = new string[] { "&", "&&" };
        operatorPrecedences[13] = new string[] { "|", "||" };
        operatorPrecedences[iOperatorsTilda] = new string[] { "~" };          // unary or binary
        operatorPrecedences[iOperatorsRightAssignment] = new string[] { "->", "->>" };
        operatorPrecedences[iOperatorsLeftAssignment1] = new string[] { "<-", "<<-" };
        operatorPrecedences[iOperatorsLeftAssignment2] = new string[] { "=" };
        operatorPrecedences[18] = new string[] { "?", "??" };

        var tokenTreeList = new List<RToken>();
        int iPos = 0;
        while (iPos < tokenList.Count)
        {
            // create list of tokens for this statement
            uint statementScriptPos = tokenList[iPos].ScriptPos;
            var statementTokens = new List<RToken>();
            while (iPos < tokenList.Count)
            {
                statementTokens.Add(tokenList[iPos]);
                iPos++;
                // we don't add this termination condition to the while statement because we also want the token that terminates the statement 
                //todo if (lstTokens[iPos].Tokentype == RToken.TokenType.REndStatement || lstTokens[iPos].Tokentype == RToken.TokenType.REndScript)
                if (tokenList[iPos - 1].TokenType == RToken.TokenTypes.REndStatement)
                {
                    break;
                }
            }

            // restructure the list into a token tree
            var lstTokenPresentation = GetLstPresentation(statementTokens, 0);
            int argiPos = 0;
            var lstTokenBrackets = GetLstTokenBrackets(lstTokenPresentation, ref argiPos);
            var lstTokenFunctionBrackets = GetLstTokenFunctionBrackets(lstTokenBrackets);
            int argiPos1 = 0;
            var lstTokenFunctionCommas = GetLstTokenFunctionCommas(lstTokenFunctionBrackets, ref argiPos1);
            var lstTokenTree = GetLstTokenOperators(lstTokenFunctionCommas);

            if (lstTokenTree.Count == 0
                || (lstTokenTree.Count == 1 && iPos < tokenList.Count)
                || lstTokenTree.Count > 2)
            {
                throw new Exception("The token tree for a statement must contain a single token followed by an endStatement token. Special case: for the last staement in the script, an endStatement token is optional.");
            }
            tokenTreeList.Add(lstTokenTree[0].CloneMe());
            if (lstTokenTree.Count > 1)
            {
                tokenTreeList.Add(lstTokenTree[1].CloneMe());
            }
        }
        return tokenTreeList;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="lstTokens"/>, from position 
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
    /// <param name="lstTokens">   The list of tokens to process. </param>
    /// <param name="iPos">        The position in the list to start processing </param>
    /// 
    /// <returns>   A token tree where presentation information is stored as a child of the next 
    ///             non-presentation element. </returns>
    /// --------------------------------------------------------------------------------------------
    private static List<RToken> GetLstPresentation(List<RToken> lstTokens, int iPos)
    {

        if (lstTokens.Count < 1)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        string strPrefix = "";
        uint prefixScriptPos = 0;

        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];
            iPos += 1;
            switch (clsToken.TokenType)
            {
                case RToken.TokenTypes.RSpace:
                case RToken.TokenTypes.RComment:
                case RToken.TokenTypes.RNewLine:
                    {
                        if (string.IsNullOrEmpty(strPrefix))
                        {
                            prefixScriptPos = clsToken.ScriptPos;
                        }
                        strPrefix += clsToken.Lexeme.Text;                        
                        break;
                    }

                default:
                    {
                        if (!string.IsNullOrEmpty(strPrefix))
                        {
                            clsToken.ChildTokens.Add(new RToken(new RLexeme(strPrefix), prefixScriptPos, RToken.TokenTypes.RPresentation));
                        }
                        lstTokensNew.Add(clsToken.CloneMe());
                        strPrefix = "";
                        prefixScriptPos = 0;
                        break;
                    }
            }
        }

        // Edge case: if there is still presentation information not yet added to a tree element
        // (this may happen if the last statement in the script is not terminated 
        // with a new line or '}')
        if (!string.IsNullOrEmpty(strPrefix))
        {
            clsToken = new RToken(new RLexeme(""), prefixScriptPos, RToken.TokenTypes.RInvalid);
            lstTokensNew.Add(clsToken);

            // add a new end statement token that contains the presentation information
            clsToken = new RToken(new RLexeme(""), prefixScriptPos + (uint)strPrefix.Length, RToken.TokenTypes.REndStatement);
            clsToken.ChildTokens.Add(new RToken(new RLexeme(strPrefix), prefixScriptPos, RToken.TokenTypes.RPresentation));
            lstTokensNew.Add(clsToken);
        }

        return lstTokensNew;
    }


    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Iterates through the tokens in <paramref name="lstTokens"/> from position 
    /// <paramref name="iPos"/>. If the token is a '(' then it makes everything inside the brackets a 
    /// child of the '(' token. If the '(' belongs to a function then makes the '(' a child of the 
    /// function. Brackets may be nested. For example, '(a*(b+c))' is structured as:<code>
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
    /// <param name="lstTokens">   The token tree to restructure. </param>
    /// <param name="iPos">        [in,out] The position in the list to start processing </param>
    /// 
    /// <returns>   A token tree restructured for round brackets. </returns>
    /// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenBrackets(List<RToken> lstTokens, ref int iPos)
    {

        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];
            iPos += 1;
            switch (clsToken.Lexeme.Text ?? "")
            {
                case "(":
                    {
                        var lstTokensTmp = GetLstTokenBrackets(lstTokens, ref iPos);
                        foreach (RToken clsTokenChild in lstTokensTmp)
                        {
                            if (clsTokenChild == null)
                            {
                                throw new Exception("Token has illegal empty child.");
                            }
                            clsToken.ChildTokens.Add(clsTokenChild.CloneMe());
                        }

                        break;
                    }
                case ")":
                    {
                        lstTokensNew.Add(clsToken.CloneMe());
                        return lstTokensNew;
                    }
            }
            lstTokensNew.Add(clsToken.CloneMe());
        }
        return lstTokensNew;
    }

    // TODO move to RTokenList class?
    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the tree of tokens in <paramref name="lstTokens"/>. If the token is a function name then it 
    /// makes the subsequent '(' a child of the function name token. </summary>
    /// 
    /// <param name="lstTokens">   The token tree to restructure. </param>
    /// 
    /// <returns>   A token tree restructured for function names. </returns>
    /// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenFunctionBrackets(List<RToken> lstTokens)
    {

        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        int iPos = 0;
        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];

            if (clsToken.TokenType == RToken.TokenTypes.RFunctionName)
            {
                // if next steps will go out of bounds, then throw developer error
                if (iPos > lstTokens.Count - 2)
                {
                    throw new Exception("The function's parameters have an unexpected format and cannot be processed.");
                }
                // make the function's open bracket a child of the function name
                iPos += 1;
                clsToken.ChildTokens.Add(lstTokens[iPos].CloneMe());
            }
            clsToken.ChildTokens = GetLstTokenFunctionBrackets(clsToken.CloneMe().ChildTokens);
            lstTokensNew.Add(clsToken.CloneMe());
            iPos += 1;
        }
        return lstTokensNew;
    }

    // TODO move to RTokenList class?
    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the tree of tokens in <paramref name="lstTokens"/>. If the token is a ',' that 
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
    /// <param name="lstTokens">        The token tree to restructure. </param>
    /// <param name="iPos">             [in,out] The position in the list to start processing </param>
    /// <param name="bProcessingComma"> (Optional) True if function called when already processing 
    ///     a comma (prevents commas being nested inside each other). </param>
    /// 
    /// <returns>   A token tree restructured for function commas. </returns>
    /// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenFunctionCommas(List<RToken> lstTokens, ref int iPos, bool bProcessingComma = false)
    {
        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        var lstOpenBrackets = new List<string>() { "[", "[[" };
        var lstCloseBrackets = new List<string>() { "]", "]]" };
        int iNumOpenBrackets = 0;

        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];

            // only process commas that separate function parameters,
            // ignore commas inside square bracket (e.g. `a[b,c]`)
            iNumOpenBrackets += lstOpenBrackets.Contains(clsToken.Lexeme.Text) ? 1 : 0;
            iNumOpenBrackets -= lstCloseBrackets.Contains(clsToken.Lexeme.Text) ? 1 : 0;
            if (iNumOpenBrackets == 0 && clsToken.Lexeme.Text == ",")
            {
                if (bProcessingComma)
                {
                    iPos -= 1;  // ensure this comma is processed in the level above
                    return lstTokensNew;
                }
                else
                {
                    iPos += 1;
                    clsToken.ChildTokens = clsToken.ChildTokens.Concat(GetLstTokenFunctionCommas(lstTokens, ref iPos, true)).ToList();
                }
            }
            else
            {
                int argiPos = 0;
                clsToken.ChildTokens = GetLstTokenFunctionCommas(clsToken.CloneMe().ChildTokens, ref argiPos);
            }

            lstTokensNew.Add(clsToken);
            iPos += 1;
        }
        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary> 
    /// Iterates through all the possible operators in order of precedence. For each operator, 
    /// traverses the tree of tokens in <paramref name="lstTokens"/>. If the operator is found then 
    /// the operator's parameters (typically the tokens to the left and right of the operator) are 
    /// made children of the operator. For example, 'a*b+c' is structured as:<code>
    ///   +<para>
    ///   ..*</para><para>
    ///   ....a</para><para>
    ///   ....b</para><para>
    ///   ..c</para></code></summary>
    /// 
    /// <param name="lstTokens">   The token tree to restructure. </param>
    /// 
    /// <returns>   A token tree restructured for all the possible operators. </returns>
    /// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenOperators(List<RToken> lstTokens)
    {
        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        for (int iPosOperators = 0, loopTo = Information.UBound(operatorPrecedences) - 1; iPosOperators <= loopTo; iPosOperators++)
        {

            // restructure the tree for the next group of operators in the precedence list
            lstTokensNew = GetLstTokenOperatorGroup(lstTokens, iPosOperators);

            // clone the new tree before restructuring for the next operator
            lstTokens = new List<RToken>();
            foreach (RToken clsTokenTmp in lstTokensNew)
                lstTokens.Add(clsTokenTmp.CloneMe());
        }

        return lstTokensNew;
    }

    // TODO move to RTokenList class?
    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Traverses the tree of tokens in <paramref name="lstTokens"/>. If one of the operators in 
    /// the <paramref name="iPosOperators"/> group is found, then the operator's parameters 
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
    /// <param name="lstTokens">        The token tree to restructure. </param>
    /// <param name="iPosOperators">  The group of operators to search for in the tree. </param>
    /// 
    /// <returns>   A token tree restructured for the specified group of operators. </returns>
    /// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenOperatorGroup(List<RToken> lstTokens, int iPosOperators)
    {

        if (lstTokens.Count < 1)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        RToken? clsTokenPrev = null;
        bool bPrevTokenProcessed = false;

        int iPosTokens = 0;
        while (iPosTokens < lstTokens.Count)
        {
            clsToken = lstTokens[iPosTokens].CloneMe();

            // if the token is the operator we are looking for and it has not been processed already
            // Edge case: if the operator already has (non-presentation) children then it means 
            // that it has already been processed. This happens when the child is in the 
            // same precedence group as the parent but was processed first in accordance 
            // with the left to right rule (e.g. 'a/b*c').
            if ((operatorPrecedences[iPosOperators].Contains(clsToken.Lexeme.Text) || iPosOperators == iOperatorsUserDefined && Regex.IsMatch(clsToken.Lexeme.Text, "^%.*%$")) && (clsToken.ChildTokens.Count == 0 || clsToken.ChildTokens.Count == 1 && clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation))
            {

                switch (clsToken.TokenType)
                {
                    case RToken.TokenTypes.ROperatorBracket: // handles '[' and '[['
                        {
                            if (iPosOperators != iOperatorsBrackets)
                            {
                                break;
                            }

                            // make the previous and next tokens (up to the corresponding close bracket), the children of the current token
                            if (clsTokenPrev == null)
                            {
                                throw new Exception("The bracket operator has no parameter on its left.");
                            }
                            clsToken.ChildTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
                            iPosTokens += 1;
                            string strCloseBracket = clsToken.Lexeme.Text == "[" ? "]" : "]]";
                            int iNumOpenBrackets = 1;
                            while (iPosTokens < lstTokens.Count)
                            {
                                iNumOpenBrackets += (lstTokens[iPosTokens].Lexeme.Text ?? "") == (clsToken.Lexeme.Text ?? "") ? 1 : 0;
                                iNumOpenBrackets -= (lstTokens[iPosTokens].Lexeme.Text ?? "") == (strCloseBracket ?? "") ? 1 : 0;
                                // discard the terminating cloe bracket
                                if (iNumOpenBrackets == 0)
                                {
                                    break;
                                }
                                clsToken.ChildTokens.Add(lstTokens[iPosTokens].CloneMe());
                                iPosTokens += 1;
                            }

                            break;
                        }

                    case RToken.TokenTypes.ROperatorBinary:
                        {
                            // edge case: if we are looking for unary '+' or '-' and we found a binary '+' or '-'
                            if (iPosOperators == iOperatorsUnaryOnly)
                            {
                                // do not process (binary '+' and '-' have a lower precedence and will be processed later)
                                break;
                            }
                            else if (clsTokenPrev == null)
                            {
                                throw new Exception("The binary operator has no parameter on its left.");
                            }

                            // make the previous and next tokens, the children of the current token
                            clsToken.ChildTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
                            clsToken.ChildTokens.Add(GetNextToken(lstTokens, iPosTokens));
                            iPosTokens += 1;
                            // while next token is the same operator (e.g. 'a+b+c+d...'), 
                            // then keep making the next token, the child of the current operator token
                            RToken clsTokenNext;
                            while (iPosTokens < lstTokens.Count - 1)
                            {
                                clsTokenNext = GetNextToken(lstTokens, iPosTokens);
                                if (!(clsToken.TokenType == clsTokenNext.TokenType) || !((clsToken.Lexeme.Text ?? "") == (clsTokenNext.Lexeme.Text ?? "")))
                                {
                                    break;
                                }

                                iPosTokens += 1;
                                clsToken.ChildTokens.Add(GetNextToken(lstTokens, iPosTokens));
                                iPosTokens += 1;
                            }

                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryRight:
                        {
                            // edge case: if we found a unary '+' or '-', but we are not currently processing the unary '+'and '-' operators
                            if (operatorPrecedences[iOperatorsUnaryOnly].Contains(clsToken.Lexeme.Text) && !(iPosOperators == iOperatorsUnaryOnly))
                            {
                                break;
                            }
                            // make the next token, the child of the current operator token
                            clsToken.ChildTokens.Add(GetNextToken(lstTokens, iPosTokens));
                            iPosTokens += 1;
                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryLeft:
                        {
                            if (clsTokenPrev == null || !(iPosOperators == iOperatorsTilda))
                            {
                                throw new Exception("Illegal unary left operator ('~' is the only valid unary left operator).");
                            }
                            // make the previous token, the child of the current operator token
                            clsToken.ChildTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
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
            if (!bPrevTokenProcessed && !(clsTokenPrev == null))
            {
                // add the previous token to the tree
                lstTokensNew.Add(clsTokenPrev);
            }

            // process the current token's children
            clsToken.ChildTokens = GetLstTokenOperatorGroup(clsToken.CloneMe().ChildTokens, iPosOperators);

            clsTokenPrev = clsToken.CloneMe();
            bPrevTokenProcessed = false;
            iPosTokens += 1;
        }

        if (clsTokenPrev == null)
        {
            throw new Exception("Expected that there would still be a token to add to the tree.");
        }
        lstTokensNew.Add(clsTokenPrev.CloneMe());

        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns a clone of the next token in the <paramref name="lstTokens"/> list, 
    ///             after <paramref name="iPosTokens"/>. If there is no next token then throws 
    ///             an error.</summary>
    /// 
    /// <param name="lstTokens">      The list of tokens. </param>
    /// <param name="iPosTokens">     The position of the current token in the list. </param>
    /// 
    /// <returns>   A clone of the next token in the <paramref name="lstTokens"/> list. </returns>
    /// --------------------------------------------------------------------------------------------
    private static RToken GetNextToken(List<RToken> lstTokens, int iPosTokens)
    {
        if (iPosTokens >= lstTokens.Count - 1)
        {
            throw new Exception("Token list ended unexpectedly.");
        }
        return lstTokens[iPosTokens + 1].CloneMe();
    }


}