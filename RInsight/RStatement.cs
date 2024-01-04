using System.Linq;

namespace RInsight;

/// <summary>
/// Represents a single valid R statement.
/// </summary>
public class RStatement
{
    /// <summary> True if this statement is an assignment statement (e.g. x <- 1). </summary>
    public bool IsAssignment { get; }

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

        uint startPos = token.ScriptPosStartStatement;
        uint endPos = token.ScriptPosEndStatement;
        Text = "";
        TextNoFormatting = "";
        foreach (RToken tokenFlat in tokensFlat)
        {
            uint tokenStartPos = tokenFlat.ScriptPosStartStatement;
            if (tokenStartPos >= endPos)
            {
                break;
            }

            if (tokenStartPos >= startPos)
            {
                Text += tokenFlat.Lexeme.Text;
                TextNoFormatting += tokenFlat.IsPresentation ? "" : tokenFlat.Lexeme.Text;
            }
        }

    }

}