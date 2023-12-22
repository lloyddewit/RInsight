namespace RInsight;

/// --------------------------------------------------------------------------------------------
/// <summary>
/// Represents an R script as a list of R lexemes. Each lexeme is a string of characters that 
/// represents a valid R element (identifier, operator, keyword, seperator, bracket etc.).
/// </summary>
/// --------------------------------------------------------------------------------------------
public class RLexemeList {

    /// <summary>
    /// List of R lexemes    
    /// </summary>
    public List<RLexeme> Lexemes { get; }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Generates a list of R lexemes from <paramref name="script"/>. 
    ///             Each lexeme is a string of characters that represents a valid R element 
    ///             (identifier, operator, keyword, seperator, bracket etc.). 
    ///             <para>
    ///             This function identifies lexemes using a technique known as 'longest match' 
    ///             or 'maximal munch'. It keeps adding characters to the lexeme one at a time 
    ///             until it reaches a character that is not in the set of characters acceptable 
    ///             for that lexeme.
    ///             </para></summary>
    /// 
    /// <param name="script"> The R script to convert (must be syntactically correct R). </param>
    /// --------------------------------------------------------------------------------------------
    public RLexemeList(string script) 
    {
        Lexemes = new List<RLexeme>();
        if (script.Length == 0)
        {
            return;
        }
        string lexemeText = "";
        var bracketStack = new Stack<bool>();

        foreach (char lexemeChar in script)
        {
            // we keep adding characters to the lexeme, one at a time, until we reach a character
            // that would make the lexeme invalid.
            // Second part of condition is edge case for nested operator brackets (see note below).
            var lexemeTextExpanded = new RLexeme(lexemeText + lexemeChar);
            if (lexemeTextExpanded.IsValid &&
                !(lexemeTextExpanded.Text == "]]" 
                  && (bracketStack.Count < 1 || bracketStack.Peek())))
            {
                lexemeText += lexemeChar;
                continue;
            }
            // Edge case: We need to handle nested operator brackets e.g. 'k[[l[[m[6]]]]]'. 
            // For the above example, we need to recognise that the ']' to the right 
            // of '6' is a single ']' bracket and is not part of a double ']]' bracket.
            // To achieve this, we push each open bracket to a stack so that we know 
            // which type of closing bracket is expected for each open bracket.
            switch (lexemeText)
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
                            throw new Exception("Closing bracket detected ('" + lexemeText 
                                                + "') with no corresponding open bracket.");
                        }
                        bracketStack.Pop();
                        break;
                    }
            }
            // adding the new char to the lexeme would make the lexeme invalid, 
            // so we add the existing lexeme to the list and start a new lexeme
            Lexemes.Add(new RLexeme(lexemeText));
            lexemeText = lexemeChar.ToString();
        }
        // add the final lexeme to the list
        var finalLexeme = new RLexeme(lexemeText);
        if (!finalLexeme.IsValid)
        {
            throw new Exception("Final lexeme ('" + finalLexeme.Text 
                                + "') is not a valid lexeme.");
        }
        Lexemes.Add(finalLexeme);
    }
}