using System.Collections.Generic;

namespace RInsight;

public class clsRElementKeyWord : clsRElement
{

    public List<clsRParameter> lstRParameters = new List<clsRParameter>();
    public clsRScript clsScript;

    public clsRElementKeyWord(clsRToken clsToken, bool bBracketedNew = false) : base(clsToken, bBracketedNew)
    {
    }

    // Public clsObject As Object 'if statement part in '()' that returns true or false
    // fn: argument definition (also in '()')
    // else: ! of if?

}