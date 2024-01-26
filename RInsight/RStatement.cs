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
    /// Constructs an object representing a valid R statement from the <paramref name="token"/> 
    /// token tree. </summary>
    /// 
    /// <param name="token">      The tree of R tokens to process </param>
    /// <param name="tokensFlat"> A one-dimensional list of all the tokens in the script 
    ///                           containing <paramref name="token"/> (useful for conveniently 
    ///                           reconstructing the text representation of the statement).</param>
    /// --------------------------------------------------------------------------------------------
    public RStatement(RToken token, List<RToken> tokensFlat)
    {
        var assignments = new HashSet<string> { "->", "->>", "<-", "<<-", "=" };
        IsAssignment = token.TokenType == RToken.TokenTypes.ROperatorBinary && assignments.Contains(token.Lexeme.Text);

        StartPos = token.ScriptPosStartStatement;
        uint endPos = token.ScriptPosEndStatement;
        int firstNewLineIndex = 0;
        bool tokenPrevIsEndStatement = false;
        Text = "";
        TextNoFormatting = "";
        foreach (RToken tokenFlat in tokensFlat)
        {
            uint tokenStartPos = tokenFlat.ScriptPosStartStatement;
            if (tokenStartPos < StartPos)
            {
                tokenPrevIsEndStatement = tokenFlat.TokenType == RToken.TokenTypes.REndStatement;
                continue;
            }
            if (tokenStartPos >= endPos)
            {
                break;
            }

            // edge case: todo
            string tokenText = tokenFlat.Lexeme.Text;
            if (Text == "" 
                && !tokenPrevIsEndStatement
                && tokenFlat.IsPresentation                 
                && tokenFlat.Lexeme.Text.Length > 0)
            {
                firstNewLineIndex = Math.Min(tokenText.IndexOf("\r"), tokenText.IndexOf("\n"));
                if (firstNewLineIndex > -1)
                {
                    tokenText = tokenText.Substring(firstNewLineIndex);
                    if (tokenText.StartsWith("\r\n"))
                    {
                        tokenText = tokenText.Substring(1);
                        firstNewLineIndex++;
                    }
                    tokenText = tokenText.Substring(1);
                    firstNewLineIndex++;
                }
            }

            Text += tokenText;
            tokenPrevIsEndStatement = tokenFlat.TokenType == RToken.TokenTypes.REndStatement;

            // for non format text, ignore presentation tokens and replace end statements with ;
            if (tokenFlat.TokenType == RToken.TokenTypes.REndStatement)
            {
                TextNoFormatting += ";";
            }
            else if (tokenFlat.TokenType == RToken.TokenTypes.RKeyWord
                     && (tokenFlat.Lexeme.Text == "else" || tokenFlat.Lexeme.Text == "in"))
            {
                TextNoFormatting += " " + tokenFlat.Lexeme.Text + " ";
            }
            else if (!tokenFlat.IsPresentation) // ignore presentation tokens
            {
                TextNoFormatting += tokenFlat.Lexeme.Text;
            }
        }
        StartPos += (uint)Math.Max(firstNewLineIndex, 0);
        // remove trailing `;` from TextNoFormatting (only needed to separate internal compound statements)
        TextNoFormatting = TextNoFormatting.Trim(';');
    }

}