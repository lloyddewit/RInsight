using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace RInsight;


// TODO Should we model constants differently to syntactic names? (there are five types of constants: integer, logical, numeric, complex and string)
// TODO Test special constants {"NULL", "NA", "Inf", "NaN"}
// TODO Test function names as string constants. E.g 'x + y can equivalently be written "+"(x, y). Notice that since '+' is a non-standard function name, it needs to be quoted (see https://cran.r-project.org/doc/manuals/r-release/R-lang.html#Writing-functions)'
// TODO handle '...' (used in function definition)
// TODO handle '.' normally just part of a syntactic name, but has a special meaning when in a function name, or when referring to data (represents no variable)
// TODO is it possible for packages to be nested (e.g. 'p1::p1_1::f()')?
// TODO currently all newlines (vbLf, vbCr and vbCrLf) are converted to vbLf. Is it important to remember what the original new line character was?
// TODO convert public data members to properties (all classes)
// TODO provide an option to get script with automatic indenting (specifiy num spaces for indent and max num Columns per line)
// 
// 17/11/20
// - allow named operator params (R-Instat allows operator params to be named, but this infor is lost in script)
// 
// 01/03/21
// - how should bracket operator separators be modelled?
// strInput = "df[1:2,]"
// strInput = "df[,1:2]"
// strInput = "df[1:2,1:2]"
// strInput = "df[1:2,""x""]"
// strInput = "df[1:2,c(""x"",""y"")]"
// 

    /// <summary>   TODO Add class summary. </summary>
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

        var lstTokens = new RTokenList(strInput).Tokens;

        if (lstTokens is null) {
            return;
        }

        int iPos = 0;
        var dctAssignments = new Dictionary<string, RStatement>();
        while (iPos < lstTokens.Count) {
            uint iScriptPos = lstTokens[iPos].ScriptPos;
            var clsStatement = new RStatement(lstTokens, ref iPos, dctAssignments);
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