using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace RInsight;
/// <summary>
/// todo
/// </summary>
public class RScript {

    /// <summary>   
    /// The R statements in the script. The dictionary key is the start position of the statement 
    /// in the script. The dictionary value is the statement itself. </summary>
    public OrderedDictionary statements = new OrderedDictionary();

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Parses the R script in <paramref name="strInput"/> and populates the distionary
    ///             of R statements.
    ///             <para>
    ///             This subroutine will accept, and correctly process all valid R. However, this 
    ///             class does not attempt to validate <paramref name="strInput"/>. If it is not 
    ///             valid R then this subroutine may still process the script without throwing an 
    ///             exception. In this case, the list of R statements will be undefined.
    ///             </para><para>
    ///             In other words, this subroutine should not generate false negatives (reject 
    ///             valid R) but may generate false positives (accept invalid R).
    ///             </para></summary>
    /// 
    /// <param name="strInput"> The R script to parse. This must be valid R according to the 
    ///                         R language specification at 
    ///                         https://cran.r-project.org/doc/manuals/r-release/R-lang.html 
    ///                         (referenced 01 Feb 2021).</param>
    /// --------------------------------------------------------------------------------------------
    public RScript(string strInput) {
        if (string.IsNullOrEmpty(strInput)) {
            return;
        }

        List<RToken> tokenList = new RTokenList(strInput).Tokens;

        int pos = 0;
        var dctAssignments = new Dictionary<string, RStatement>();
        while (pos < tokenList.Count) {
            uint iScriptPos = tokenList[pos].ScriptPosStartStatement;
            RToken tokenEndStatement;
            if (pos + 1 < tokenList.Count)
            {
                tokenEndStatement = tokenList[pos + 1];
            }
            else
            {
                tokenEndStatement = new RToken(new RLexeme(""), iScriptPos+1, RToken.TokenTypes.REndStatement);
            }
            var clsStatement = new RStatement(tokenList[pos], tokenEndStatement, dctAssignments);
            pos += 2;
            statements.Add(iScriptPos, clsStatement);

            // if the value of an assigned element is new/updated
            if (!(clsStatement.clsAssignment == null)) {
                // store the updated/new definition in the dictionary
                if (dctAssignments.ContainsKey(clsStatement.clsAssignment.strTxt)) {
                    dctAssignments[clsStatement.clsAssignment.strTxt] = clsStatement;
                } else {
                    dctAssignments.Add(clsStatement.clsAssignment.strTxt, clsStatement);
                }
            }
        }
    }

    /// --------------------------------------------------------------------------------------------
    /// <summary>   Returns this object as a valid, executable R script. </summary>
    /// 
    /// <param name="bIncludeFormatting">   If True, then include all formatting information in 
    ///     returned string (comments, indents, padding spaces, extra line breaks etc.). </param>
    /// 
    /// <returns>   The current state of this object as a valid, executable R script. </returns>
    /// --------------------------------------------------------------------------------------------
    public string GetAsExecutableScript(bool bIncludeFormatting = true) {
        string strTxt = "";
        foreach (DictionaryEntry entry in statements) {
            if (entry.Value is null) {
                throw new Exception("The dictionary entry value cannot be null.");
            }

            RStatement rStatement = (RStatement)entry.Value;
            strTxt += rStatement.GetAsExecutableScript(bIncludeFormatting) + (bIncludeFormatting ? "" : Constants.vbLf);
        }
        return strTxt;
    }

}