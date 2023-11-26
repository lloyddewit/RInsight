using Microsoft.VisualBasic;

namespace RInsight;

public class RParameter
{
    public string? strArgName; // TODO spaces around '=' as option?
    public RElement? clsArgValue;
    public RElement? clsArgValueDefault;
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