using System.Collections.Generic;
using Microsoft.VisualBasic;

namespace RInsight;

public class RElementOperator : RElementAssignable
{
    public bool bFirstParamOnRight = false;
    public string strTerminator = ""; // only used for '[' and '[[' operators
    public List<RParameter> lstParameters = new List<RParameter>();

    public RElementOperator(RToken clsToken, bool bBracketedNew = false, bool bFirstParamOnRightNew = false) : base(clsToken, null, bBracketedNew)
    {
        bFirstParamOnRight = bFirstParamOnRightNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   TODO. </summary>
/// 
/// <returns>   as debug string. </returns>
/// --------------------------------------------------------------------------------------------
    public new string GetAsDebugString()
    {
        string strTxt = "ElementOperator: " + Constants.vbLf + "bFirstParamOnRight: " + bFirstParamOnRight + Constants.vbLf + "strTerminator: " + strTerminator + Constants.vbLf + "lstRParameters" + Constants.vbLf;

        foreach (RParameter clsParameter in lstParameters)
            strTxt += clsParameter.GetAsDebugString();

        return strTxt;
    }

}