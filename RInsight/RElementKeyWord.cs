using System.Collections.Generic;

namespace RInsight;

public class RElementKeyWord : RElement
{

    public List<RParameter> lstRParameters = new List<RParameter>();
    public RScript? clsScript;

    public RElementKeyWord(RToken clsToken, bool bBracketedNew = false) : base(clsToken, bBracketedNew)
    {
    }

    // Public clsObject As Object 'if statement part in '()' that returns true or false
    // fn: argument definition (also in '()')
    // else: ! of if?

}