
namespace RInsight;

public class RElementAssignable : RElement
{

    /// <summary>   
/// The statement where this element is assigned. For example, for the following R script, on the 2nd line, the statement associated with 'a' will be 'a=1'.
/// <code>
/// a=1<para>
/// b=a</para></code></summary>
    public RStatement? clsStatement;

    public RElementAssignable(RToken clsToken, RStatement? clsStatementNew = null, bool bBracketedNew = false, string strPackagePrefix = "") : base(clsToken, bBracketedNew, strPackagePrefix)
    {
        clsStatement = clsStatementNew;
    }

}