using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class RElementFunction : RElementProperty
{

    public List<RParameter> lstParameters = new List<RParameter>();

    public RElementFunction(RToken clsToken, bool bBracketedNew = false, string strPackageNameNew = "", string strPackagePrefix = "", List<RElement>? lstObjectsNew = null) : base(clsToken, lstObjectsNew, bBracketedNew, strPackageNameNew, strPackagePrefix)
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

        foreach (RParameter clsParameter in lstParameters)
            strTxt += clsParameter.GetAsDebugString();

        return strTxt;
    }

}