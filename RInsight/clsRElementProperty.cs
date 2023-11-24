using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class clsRElementProperty : clsRElementAssignable
{

    public string strPackageName = ""; // only used for functions and variables (e.g. 'constants::syms$h')
    public List<clsRElement>? lstObjects = new List<clsRElement>(); // only used for functions and variables (e.g. 'constants::syms$h')

    public clsRElementProperty(clsRToken clsToken, List<clsRElement>? lstObjectsNew, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "") : base(GetTokenCleanedPresentation(clsToken, strPackageNameNew, lstObjectsNew), null, bBracketedNew, strPackagePrefix)
    {
        strPackageName = strPackageNameNew;
        lstObjects = lstObjectsNew ?? new List<clsRElement>();
    }

    private static clsRToken GetTokenCleanedPresentation(clsRToken clsToken, string strPackageNameNew, List<clsRElement>? lstObjectsNew)
    {
        var clsTokenNew = clsToken.CloneMe();

        // Edge case: if the object has a package name or an object list, and formatting information
        if ((!string.IsNullOrEmpty(strPackageNameNew) || !(lstObjectsNew == null) && lstObjectsNew.Count > 0) && !(clsToken.lstTokens == null) && clsToken.lstTokens.Count > 0 && clsToken.lstTokens[0].enuToken == clsRToken.typToken.RPresentation)
        {
            // remove any formatting information associated with the main element.
            // This is needed to pass test cases such as:
            // 'pkg ::  obj1 $ obj2$ fn1 ()' should be displayed as 'pkg::obj1$obj2$fn1()'
            clsTokenNew.lstTokens[0].strTxt = "";
        }

        return clsTokenNew;
    }

}