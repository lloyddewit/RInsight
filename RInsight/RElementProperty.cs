using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class RElementProperty : RElementAssignable
{

    public string strPackageName = ""; // only used for functions and variables (e.g. 'constants::syms$h')
    public List<RElement>? lstObjects = new List<RElement>(); // only used for functions and variables (e.g. 'constants::syms$h')

    // todo    public RElementProperty(RToken clsToken, List<RElement>? lstObjectsNew, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "") : base(GetTokenCleanedPresentation(clsToken, strPackageNameNew, lstObjectsNew), null, bBracketedNew, strPackagePrefix)
    public RElementProperty(RToken clsToken, List<RElement>? lstObjectsNew, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "") : 
        base(clsToken, null, bBracketedNew, strPackagePrefix)
    {
        strPackageName = strPackageNameNew;
        lstObjects = lstObjectsNew ?? new List<RElement>();
    }

    //TODO DELETE?
    //private static RToken GetTokenCleanedPresentation(RToken clsToken, string strPackageNameNew, List<RElement>? lstObjectsNew)
    //{
    //    var clsTokenNew = clsToken.CloneMe();

    //    // Edge case: if the object has a package name or an object list, and formatting information
    //    if ((!string.IsNullOrEmpty(strPackageNameNew) || !(lstObjectsNew == null) && lstObjectsNew.Count > 0) && !(clsToken.ChildTokens == null) && clsToken.ChildTokens.Count > 0 && clsToken.ChildTokens[0].Tokentype == RToken.TokenType.RPresentation)
    //    {
    //        // remove any formatting information associated with the main element.
    //        // This is needed to pass test cases such as:
    //        // 'pkg ::  obj1 $ obj2$ fn1 ()' should be displayed as 'pkg::obj1$obj2$fn1()'
    //        clsTokenNew.ChildTokens[0].Lexeme = new RLexeme("");
    //    }

    //    return clsTokenNew;
    //}

}