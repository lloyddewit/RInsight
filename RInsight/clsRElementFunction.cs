using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class clsRElementFunction : clsRElementProperty
{

    public List<clsRParameter> lstParameters = new List<clsRParameter>();

    public clsRElementFunction(clsRToken clsToken, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "", List<clsRElement>? lstObjectsNew = null) : base(clsToken, lstObjectsNew, bBracketedNew, strPackageNameNew, strPackagePrefix)
    {
    }



    /// --------------------------------------------------------------------------------------------
/// <summary>   TODO. </summary>
/// 
/// <returns>   as debug string. </returns>
/// --------------------------------------------------------------------------------------------
    public new string GetAsDebugString()
    {
        string strTxt = "ElementFunction: " + Constants.vbLf;

        foreach (clsRParameter clsParameter in lstParameters)
            strTxt += clsParameter.GetAsDebugString();

        return strTxt;
    }

}