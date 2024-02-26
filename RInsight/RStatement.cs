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
    /// Constructs an object representing a valid R statement from the <paramref name="token"/> 
    /// token tree. </summary>
    /// 
    /// <param name="token">  The tree of R tokens to process </param>
    /// <param name="tokensFlat"> A one-dimensional list of all the tokens in the script 
    ///                           containing <paramref name="token"/> (useful for conveniently 
    ///                           reconstructing the text representation of the statement).</param>
    /// --------------------------------------------------------------------------------------------
    public RStatement(RToken token, List<RToken> tokensFlat)
    {
        var assignments = new HashSet<string> { "->", "->>", "<-", "<<-", "=" };
        IsAssignment = token.TokenType == RToken.TokenTypes.ROperatorBinary 
                       && assignments.Contains(token.Lexeme.Text);

        StartPos = token.ScriptPosStartStatement;
        uint endPos = token.ScriptPosEndStatement;
        TextNoFormatting = GetTextNoFormatting(tokensFlat, StartPos, endPos);

        // create a lossless text representation of the statement including all presentation
        // information (e.g. spaces, newlines, comments etc.)
        int startPosAdjustment = 0;
        bool tokenPrevIsEndStatement = false;
        bool firstNewLineFound = false;
        Text = "";
        foreach (RToken tokenFlat in tokensFlat)
        {
            if (tokenFlat.TokenType == RToken.TokenTypes.REmpty) continue;

            uint tokenStartPos = tokenFlat.ScriptPosStartStatement;
            if (tokenStartPos < StartPos)
            {
                tokenPrevIsEndStatement = tokenFlat.TokenType == RToken.TokenTypes.REndStatement;
                continue;
            }
            string tokenText = tokenFlat.Lexeme.Text;
            if (tokenStartPos >= endPos)
            {
                // if next statement has presentation text that belongs with the current statement
                if (!tokenPrevIsEndStatement
                    && tokenFlat.IsPresentation
                    && tokenText.Length > 0)
                {
                    Text += tokenText;
                    if (tokenFlat.Lexeme.IsNewLine)
                    {
                        break;
                    }
                    continue;
                }
                break;
            }

            // ignore any presentation characters that belong to the previous statement
            if (Text == ""
                && StartPos != 0
                && !tokenPrevIsEndStatement 
                && !firstNewLineFound
                && tokenFlat.IsPresentation
                && tokenText.Length > 0)
            {
                if (tokenFlat.Lexeme.IsNewLine)
                {
                    firstNewLineFound = true;
                }
                startPosAdjustment += tokenText.Length;
                tokenText = "";
            }
            Text += tokenText;
            tokenPrevIsEndStatement = tokenFlat.TokenType == RToken.TokenTypes.REndStatement;
        }
        StartPos += (uint)startPosAdjustment;
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>
    /// Returns a text representation of the statement, excluding all formatting information 
    /// (e.g. spaces, newlines, comments etc.).
    /// </summary>
    /// <param name="tokensFlat"> Flat list of all the tokens in the script</param>
    /// <param name="posStart">   The start position of the statement in the script</param>
    /// <param name="posEnd">     The end position of the statement in the script</param>
    /// <returns>                 A text representation of the statement, excluding all formatting
    ///                           information</returns>
    /// --------------------------------------------------------------------------------------------
    private string GetTextNoFormatting(List<RToken> tokensFlat, 
                                       uint posStart, uint posEnd)
    {
        string text = "";
        foreach (RToken token in tokensFlat)
        {
            if (token.TokenType == RToken.TokenTypes.REmpty) continue;

            uint tokenStartPos = token.ScriptPosStartStatement;
            if (tokenStartPos < posStart) continue;
            if (tokenStartPos >= posEnd) break;

            if (token.TokenType == RToken.TokenTypes.REndStatement)
            {
                text += ";";
            }
            else if (token.TokenType == RToken.TokenTypes.RKeyWord
                     && (token.Lexeme.Text == "else" 
                         || token.Lexeme.Text == "in" 
                         || token.Lexeme.Text == "repeat"))
            {
                text += " " + token.Lexeme.Text + " ";
            }
            else if (!token.IsPresentation) // ignore presentation tokens
            {
                text += token.Lexeme.Text;
            }
        }
        // remove final trailing `;` (only needed to separate internal compound statements)
        text = text.Trim(';');
        return text;
    }
}