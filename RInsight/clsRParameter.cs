using Microsoft.VisualBasic;

namespace RInsight;

public class clsRParameter
{
    public string? strArgName; // TODO spaces around '=' as option?
    public clsRElement? clsArgValue;
    public clsRElement? clsArgValueDefault;
    public int iArgPos;
    public int iArgPosDefinition;
    public string strPrefix = "";

    /// --------------------------------------------------------------------------------------------
/// <summary>   TODO. </summary>
/// 
/// <returns>   as debug string. </returns>
/// --------------------------------------------------------------------------------------------
    public string GetAsDebugString()
    {
        return "Parameter: " + Constants.vbLf + "clsArgValue: " + clsArgValue?.GetAsDebugString() + Constants.vbLf + "strPrefix: " + strPrefix + Constants.vbLf;
    }

}