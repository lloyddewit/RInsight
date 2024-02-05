using System.Linq;

namespace RInsight;

/// <summary>
/// Represents a single valid R statement.
/// </summary>
public class RStatement
{
    /// <summary> True if this statement is an assignment statement (e.g. x <- 1). </summary>
    public bool IsAssignment { get; }

    /// <summary> The position in the script where this statement starts. </summary>
    public uint StartPos { get; }

    /// <summary>
    /// The text representation of this statement, including all formatting information (comments,
    /// spaces, extra newlines etc.).
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The text representation of this statement, excluding all formatting information (comments,
    /// spaces, extra newlines etc.).
    /// </summary>
    public string TextNoFormatting{ get; }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// todo move down in class
    /// todo return position after the first newline or carriage return ('n', '\r' or '\r\n'). If neither is found, then return 0.
    /// Examples:<para>
    /// '' returns -1</para><para>
    /// '\n' returns 1</para><para>
    /// 'a' returns -1</para><para>
    /// 'a\r' returns 2</para><para>
    /// 'a\n" returns 2</para><para>
    /// "a\r\n" returns 3</para><para>
    /// "a\r\nb" returns 3</para><para>
    /// "abc\r\nd" returns 5</para><para>
    /// "abc\r\ndef" returns 5</para>
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    /// --------------------------------------------------------------------------------------------
    private int GetPresentationSplitPos(string text)
    {
        int posNewLine = text.IndexOf("\n");
        int posCarriageReturn = text.IndexOf("\r");

        if (posNewLine == -1 && posCarriageReturn == -1) return 0;
        if (posNewLine == -1) return posCarriageReturn + 1;
        if (posCarriageReturn == -1) return posNewLine + 1;

        int pos = Math.Min(posNewLine, posCarriageReturn);
        if (text.Substring(pos).StartsWith("\r\n"))
        {
            pos++;
        }
        return pos + 1;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Constructs an object representing a valid R statement from the <paramref name="tokenTree"/> 
    /// token tree. </summary>
    /// 
    /// <param name="tokenTree">      The tree of R tokens to process </param>
    /// <param name="tokensFlat"> A one-dimensional list of all the tokens in the script 
    ///                           containing <paramref name="tokenTree"/> (useful for conveniently 
    ///                           reconstructing the text representation of the statement).</param>
    /// --------------------------------------------------------------------------------------------
    public RStatement(RToken tokenTree, List<RToken> tokensFlat)
    {
        var assignments = new HashSet<string> { "->", "->>", "<-", "<<-", "=" };
        IsAssignment = tokenTree.TokenType == RToken.TokenTypes.ROperatorBinary && assignments.Contains(tokenTree.Lexeme.Text);

        StartPos = tokenTree.ScriptPosStartStatement;
        uint endPos = tokenTree.ScriptPosEndStatement;
        int startPosAdjustment = 0;
        bool tokenPrevIsEndStatement = false;
        bool firstNewLineFound = false;
        Text = "";
        TextNoFormatting = "";
        foreach (RToken token in tokensFlat)
        {
            //todo
            if (token.TokenType == RToken.TokenTypes.REmpty) continue;

            uint tokenStartPos = token.ScriptPosStartStatement;
            if (tokenStartPos < StartPos)
            {
                tokenPrevIsEndStatement = token.TokenType == RToken.TokenTypes.REndStatement;
                continue;
            }
            string tokenText = token.Lexeme.Text;
            if (tokenStartPos >= endPos)
            {
                //todo check if this token has some presentation text that belongs with the current statement
                if (!tokenPrevIsEndStatement
                    && (token.IsPresentation || token.Lexeme.IsNewLine) //todo need 2nd part of check because some newlines are endstatements
                    && tokenText.Length > 0)
                {
                    //int splitPos = GetPresentationSplitPos(tokenText);
                    //tokenText = tokenText.Substring(0, splitPos);
                    Text += tokenText;
                    if (token.Lexeme.IsNewLine)
                    {
                        TextNoFormatting += ";";
                        break;
                    }
                    continue;
                }
                break;
            }

            // edge case: todo ignore any presentation characters that belong to the previous statement
            if (Text == ""
                && StartPos != 0
                && !tokenPrevIsEndStatement 
                && !firstNewLineFound
                && (token.IsPresentation || token.Lexeme.IsNewLine) //todo need 2nd part of check because some newlines are endstatements                 
                && tokenText.Length > 0)
            {
                if (token.Lexeme.IsNewLine)
                {
                    firstNewLineFound = true;
                }
                startPosAdjustment += tokenText.Length;
                tokenText = "";
                //int splitPos = GetPresentationSplitPos(tokenText);
                //tokenText = tokenText.Substring(splitPos);
                //startPosAdjustment = splitPos;
            }
            Text += tokenText;
            tokenPrevIsEndStatement = token.TokenType == RToken.TokenTypes.REndStatement;

            // for non format text, ignore presentation tokens and replace end statements with ;
            if (token.TokenType == RToken.TokenTypes.REndStatement)
            {
                TextNoFormatting += ";";
            }
            else if (token.TokenType == RToken.TokenTypes.RKeyWord
                     && (tokenText == "else" || tokenText == "in"))
            {
                TextNoFormatting += " " + tokenText + " ";
            }
            else if (!token.IsPresentation) // ignore presentation tokens
            {
                TextNoFormatting += tokenText;
            }
        }
        // remove trailing `;` from TextNoFormatting (only needed to separate internal compound statements)
        TextNoFormatting = TextNoFormatting.Trim(';');
        StartPos += (uint)startPosAdjustment;
    }

}