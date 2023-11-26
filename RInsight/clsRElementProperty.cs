using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class RElementProperty : RElementAssignable
{

    public string strPackageName = ""; // only used for functions and variables (e.g. 'constants::syms$h')
    public List<RElement>? lstObjects = new List<RElement>(); // only used for functions and variables (e.g. 'constants::syms$h')

    public RElementProperty(RToken clsToken, List<RElement>? lstObjectsNew, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "") : base(GetTokenCleanedPresentation(clsToken, strPackageNameNew, lstObjectsNew), null, bBracketedNew, strPackagePrefix)
    {
        strPackageName = strPackageNameNew;
        lstObjects = lstObjectsNew ?? new List<RElement>();
    }

    private static RToken GetTokenCleanedPresentation(RToken clsToken, string strPackageNameNew, List<RElement>? lstObjectsNew)
    {
        var clsTokenNew = clsToken.CloneMe();

        // Edge case: if the object has a package name or an object list, and formatting information
        if ((!string.IsNullOrEmpty(strPackageNameNew) || !(lstObjectsNew == null) && lstObjectsNew.Count > 0) && !(clsToken.lstTokens == null) && clsToken.lstTokens.Count > 0 && clsToken.lstTokens[0].enuToken == RToken.typToken.RPresentation)
        {
            // remove any formatting information associated with the main element.
            // This is needed to pass test cases such as:
            // 'pkg ::  obj1 $ obj2$ fn1 ()' should be displayed as 'pkg::obj1$obj2$fn1()'
            clsTokenNew.lstTokens[0].strTxt = "";
        }

        return clsTokenNew;
    }

}