using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace RInsight;

/// <summary>   TODO Add class summary. </summary>
public class RStatement
{

    /// <summary>   If true, then when this R statement is converted to a script, then it will be 
///             terminated with a newline (else if false then a semicolon)
/// </summary>
    public bool bTerminateWithNewline = true;

    /// <summary>   The assignment operator used in this statement (e.g. '=' in the statement 'a=b').
///             If there is no assignment (e.g. as in 'myFunction(a)' then set to 'nothing'. </summary>
    public string? strAssignmentOperator;

    /// <summary>   If this R statement is converted to a script, then contains the formatting 
///             string that will prefix the assignment operator.
///             This is typically used to insert spaces before the assignment operator to line 
///             up the assignment operators in a list of assignments. For example:
///             <code>
///             shortName    = 1 <para>
///             veryLongName = 2 </para></code>
///             </summary>
    public string? strAssignmentPrefix;

    /// <summary>   If this R statement is converted to a script, then contains the formatting 
///             string that will be placed at the end of the statement.
///             This is typically used to insert a comment at the end of the statement. 
///             For example:
///             <code>
///             a = b * 2 # comment1</code>
///             </summary>
    public string? strSuffix;

    /// <summary>   The element assigned to by the statement (e.g. 'a' in the statement 'a=b').
///             If there is no assignment (e.g. as in 'myFunction(a)' then set to 'nothing'. </summary>
    public RElement? clsAssignment = null;

    /// <summary>   The element assigned in the statement (e.g. 'b' in the statement 'a=b').
///             If there is no assignment (e.g. as in 'myFunction(a)' then set to the top-
///             level element in the statement (e.g. 'myFunction'). </summary>
    public RElement? clsElement;

    /// <summary>   The relative precedence of the R operators. This is a two-dimensional array 
///             because the operators are stored in groups together with operators that 
///             have the same precedence.</summary>
    private readonly string[][] operatorPrecedences = new string[20][];

    // Constants for operator precedence groups that have special characteristics (e.g. must be unary)
    private readonly int iOperatorsBrackets = 2;
    private readonly int iOperatorsUnaryOnly = 4;
    private readonly int iOperatorsUserDefined = 6;
    private readonly int iOperatorsTilda = 14;
    private readonly int iOperatorsRightAssignment = 15;
    private readonly int iOperatorsLeftAssignment1 = 16;
    private readonly int iOperatorsLeftAssignment2 = 17;

    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Constructs an object representing a valid R statement.<para>
    /// Processes the tokens from <paramref name="lstTokens"/> from position <paramref name="iPos"/> 
    /// to the end of statement, end of script or end of list (whichever comes first).</para></summary>
    /// 
    /// <param name="lstTokens">   The list of R tokens to process </param>
    /// <param name="iPos">      [in,out] The position in the list to start processing </param>
    /// <param name="dctAssignments">   A dictionary of assignments in the parent script.</param>
    /// --------------------------------------------------------------------------------------------
    public RStatement(List<RToken> lstTokens, ref int iPos, Dictionary<string, RStatement> dctAssignments)
    {

        // if nothing to process then exit
        if (lstTokens.Count <= 0)
        {
            return;
        }

        operatorPrecedences[0] = new string[] { "::", ":::" };
        operatorPrecedences[1] = new string[] { "$", "@" };
        operatorPrecedences[iOperatorsBrackets] = new string[] { "[", "[[" }; // bracket operators
        operatorPrecedences[3] = new string[] { "^" };                          // right to left precedence
        operatorPrecedences[iOperatorsUnaryOnly] = new string[] { "-", "+" }; // unary operarors
        operatorPrecedences[5] = new string[] { ":" };
        operatorPrecedences[iOperatorsUserDefined] = new string[] { "%" };    // any operator that starts with '%' (including user-defined operators)
        operatorPrecedences[7] = new string[] { "|>" };
        operatorPrecedences[8] = new string[] { "*", "/" };
        operatorPrecedences[9] = new string[] { "+", "-" };
        operatorPrecedences[10] = new string[] { "<", ">", "<>", "<=", ">=", "==", "!=" };
        operatorPrecedences[11] = new string[] { "!" };
        operatorPrecedences[12] = new string[] { "&", "&&" };
        operatorPrecedences[13] = new string[] { "|", "||" };
        operatorPrecedences[iOperatorsTilda] = new string[] { "~" };          // unary or binary
        operatorPrecedences[iOperatorsRightAssignment] = new string[] { "->", "->>" };
        operatorPrecedences[iOperatorsLeftAssignment1] = new string[] { "<-", "<<-" };
        operatorPrecedences[iOperatorsLeftAssignment2] = new string[] { "=" };
        operatorPrecedences[18] = new string[] { "?", "??" };

        // create list of tokens for this statement
        var lstStatementTokens = new List<RToken>();
        while (iPos < lstTokens.Count)
        {
            lstStatementTokens.Add(lstTokens[iPos]);
            if (lstTokens[iPos].tokentype == RToken.TokenTypes.REndStatement || lstTokens[iPos].tokentype == RToken.TokenTypes.REndScript) // we don't add this termination condition to the while statement 
                                                                                                                                           // because we also want the token that terminates the statement 
            {
                iPos += 1;                                                              // to be part of the statement's list of tokens
                break;
            }
            iPos += 1;
        }

        // restructure the list into a token tree
        var lstTokenPresentation = GetLstPresentation(lstStatementTokens, 0);
        int argiPos = 0;
        var lstTokenBrackets = GetLstTokenBrackets(lstTokenPresentation, ref argiPos);
        var lstTokenFunctionBrackets = GetLstTokenFunctionBrackets(lstTokenBrackets);
        int argiPos1 = 0;
        var lstTokenFunctionCommas = GetLstTokenFunctionCommas(lstTokenFunctionBrackets, ref argiPos1);
        var lstTokenTree = GetLstTokenOperators(lstTokenFunctionCommas);

        // if the tree does not include at least one token, then raise development error
        if (lstTokenTree.Count < 1)
        {
            throw new Exception("The token tree must contain at least one token.");
        }

        // if the statement includes an assignment, then construct the assignment element
        if (lstTokenTree[0].tokentype == RToken.TokenTypes.ROperatorBinary && lstTokenTree[0].childTokens.Count > 1)
        {

            var clsTokenChildLeft = lstTokenTree[0].childTokens[lstTokenTree[0].childTokens.Count - 2];
            var clsTokenChildRight = lstTokenTree[0].childTokens[lstTokenTree[0].childTokens.Count - 1];

            // if the statement has a left assignment (e.g. 'x<-value', 'x<<-value' or 'x=value')
            if (operatorPrecedences[iOperatorsLeftAssignment1].Contains(lstTokenTree[0].text) || operatorPrecedences[iOperatorsLeftAssignment2].Contains(lstTokenTree[0].text))
            {
                clsAssignment = GetRElement(clsTokenChildLeft, dctAssignments);
                clsElement = GetRElement(clsTokenChildRight, dctAssignments);
            }
            else if (operatorPrecedences[iOperatorsRightAssignment].Contains(lstTokenTree[0].text))
            {
                // else if the statement has a right assignment (e.g. 'value->x' or 'value->>x')
                clsAssignment = GetRElement(clsTokenChildRight, dctAssignments);
                clsElement = GetRElement(clsTokenChildLeft, dctAssignments);
            }
        }

        // if there was an assigment then set the assignment operator and its presentation information
        if (!(clsAssignment == null))
        {
            strAssignmentOperator = lstTokenTree[0].text;
            strAssignmentPrefix = lstTokenTree[0].childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? lstTokenTree[0].childTokens[0].text : "";
        }
        else // if there was no assignment, then build the main element from the token tree's top element
        {
            clsElement = GetRElement(lstTokenTree[0], dctAssignments);
        }

        // if statement ends with a semicolon or newline
        var clsTokenEndStatement = lstTokenTree[lstTokenTree.Count - 1];
        if (clsTokenEndStatement.tokentype == RToken.TokenTypes.REndStatement || clsTokenEndStatement.tokentype == RToken.TokenTypes.REndScript)
        {
            if (clsTokenEndStatement.text == ";")
            {
                bTerminateWithNewline = false;
            }
            else // store any remaining presentation data associated with the newline
            {
                strSuffix = clsTokenEndStatement.childTokens.Count > 0 && clsTokenEndStatement.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? clsTokenEndStatement.childTokens[0].text : "";
                // do not include any trailing newlines
                strSuffix = strSuffix.EndsWith(Constants.vbLf) ? strSuffix.Substring(0, strSuffix.Length - 1) : strSuffix;
            }
        }
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   
/// Returns this object as a valid, executable R statement. <para>
/// The script may contain formatting information such as spaces, comments and extra new lines.
/// If this object was created by analysing original R script, then the returned script's 
/// formatting will be as close as possible to the original.</para><para>
/// The script may vary slightly because some formatting information is lost in the object 
/// model. For lost formatting, the formatting will be done according to the guidelines in
/// https://style.tidyverse.org/syntax.html  </para><para>
/// The returned script will always show:</para><list type="bullet"><item>
/// No spaces before commas</item><item>
/// No spaces before brackets</item><item>
/// No spaces before package ('::') and object ('$') operators</item><item>
/// One space before parameter assignments ('=')</item><item>
/// For example,  'pkg ::obj1 $obj2$fn1 (a ,b=1,    c    = 2 )' will be returned as 
///                                                 'pkg::obj1$obj2$fn1(a, b =1, c = 2)'</item>
/// </list></summary>
/// 
/// <param name="bIncludeFormatting">   If True, then include all formatting information in 
///     returned string (comments, indents, padding spaces, extra line breaks etc.). </param>
/// 
/// <returns>   The current state of this object as a valid, executable R statement. </returns>
/// --------------------------------------------------------------------------------------------
    public string GetAsExecutableScript(bool bIncludeFormatting = true)
    {
        string strScript;
        string strElement = GetScriptElement(clsElement, bIncludeFormatting);
        // if there is no assignment, then just process the statement's element
        if (clsAssignment == null || string.IsNullOrEmpty(strAssignmentOperator))
        {
            strScript = strElement;
        }
        else // else if the statement has an assignment
        {
            string strAssignment = GetScriptElement(clsAssignment, bIncludeFormatting);
            string? strAssignmentPrefixTmp = bIncludeFormatting ? strAssignmentPrefix : "";
            // if the statement has a left assignment (e.g. 'x<-value', 'x<<-value' or 'x=value')
            if (operatorPrecedences[iOperatorsLeftAssignment1].Contains(strAssignmentOperator) || operatorPrecedences[iOperatorsLeftAssignment2].Contains(strAssignmentOperator))
            {
                strScript = strAssignment + strAssignmentPrefixTmp + strAssignmentOperator + strElement;
            }
            else if (operatorPrecedences[iOperatorsRightAssignment].Contains(strAssignmentOperator))
            {
                // else if the statement has a right assignment (e.g. 'value->x' or 'value->>x')
                strScript = strElement + strAssignmentPrefixTmp + strAssignmentOperator + strAssignment;
            }
            else
            {
                throw new Exception("The statement's assignment operator is an unknown type.");
            }
        }

        if (bIncludeFormatting)
        {
            strScript += strSuffix;
            strScript += bTerminateWithNewline ? Constants.vbLf : ";";
        }

        return strScript;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns <paramref name="clsElement"/> as an executable R script. </summary>
/// 
/// <param name="clsElement">   The R element to convert to an executable R script. 
///                             The R element may be a function, operator, constant, 
///                             syntactic name, key word etc. </param>
/// 
/// <param name="bIncludeFormatting">   If True, then include all formatting information in 
///     returned string (comments, indents, padding spaces, extra line breaks etc.). </param>
/// 
/// <returns>   <paramref name="clsElement"/> as an executable R script. </returns>
/// --------------------------------------------------------------------------------------------
    private string GetScriptElement(RElement? clsElement, bool bIncludeFormatting = true)
    {

        if (clsElement == null)
        {
            return "";
        }

        string? strScript = "";
        string? strElementPrefix = Conversions.ToString(bIncludeFormatting ? clsElement.strPrefix : "");
        strScript += Conversions.ToBoolean(clsElement.bBracketed) ? "(" : "";

        switch (clsElement.GetType())
        {
            case var @case when @case == typeof(RElementFunction):
                {
                    RElementFunction clsRFunction = (RElementFunction)clsElement;

                    strScript += GetScriptElementProperty((RElementProperty)clsElement, bIncludeFormatting);
                    strScript += "(";
                    if (!(clsRFunction.lstParameters == null))
                    {
                        bool bPrefixComma = false;
                        foreach (RParameter clsRParameter in (IEnumerable)clsRFunction.lstParameters)
                        {
                            strScript += bPrefixComma ? "," : "";
                            bPrefixComma = true;
                            string strParameterPrefix = bIncludeFormatting ? clsRParameter.strPrefix : "";
                            strScript += string.IsNullOrEmpty(clsRParameter.strArgName) ? "" : strParameterPrefix + clsRParameter.strArgName + " =";
                            strScript += GetScriptElement(clsRParameter.clsArgValue, bIncludeFormatting);
                        }
                    }
                    strScript += ")";
                    break;
                }
            case var case1 when case1 == typeof(RElementProperty):
                {
                    strScript += GetScriptElementProperty((RElementProperty)clsElement, bIncludeFormatting);
                    break;
                }
            case var case2 when case2 == typeof(RElementOperator):
                {
                    RElementOperator clsROperator = (RElementOperator)clsElement;

                    if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(clsElement.strTxt, "[", false)) || Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(clsElement.strTxt, "[[", false)))
                    {
                        bool bOperatorAppended = false;
                        foreach (RParameter clsRParameter in (IEnumerable)clsROperator.lstParameters)
                        {
                            strScript += this.GetScriptElement(clsRParameter.clsArgValue, bIncludeFormatting);
                            strScript += bOperatorAppended ? "" : (strElementPrefix + clsElement.strTxt);
                            bOperatorAppended = true;
                        }

                        switch (clsElement.strTxt)
                        {
                            case "[":
                                {
                                    strScript += "]";
                                    break;
                                }
                            case "[[":
                                {
                                    strScript += "]]";
                                    break;
                                }
                        }
                    }
                    else
                    {
                        bool bPrefixOperator = Conversions.ToBoolean(clsROperator.bFirstParamOnRight);
                        foreach (RParameter clsRParameter in (IEnumerable)clsROperator.lstParameters)
                        {
                            strScript += bPrefixOperator ? (strElementPrefix + clsElement.strTxt) : "";
                            bPrefixOperator = true;
                            strScript += GetScriptElement(clsRParameter.clsArgValue, bIncludeFormatting);
                        }
                        strScript += clsROperator.lstParameters.Count == 1 && !clsROperator.bFirstParamOnRight ? strElementPrefix + clsElement.strTxt : "";
                    }

                    break;
                }
            case var case3 when case3 == typeof(RElementKeyWord): // TODO add key word functionality
                {
                    break;
                }
            case var case4 when case4 == typeof(RElement):
            case var case5 when case5 == typeof(RElementAssignable):
                {
                    strScript = Conversions.ToString(strScript + Operators.ConcatenateObject(strElementPrefix, clsElement.strTxt));
                    break;
                }
        }
        strScript += Conversions.ToBoolean(clsElement.bBracketed) ? ")" : "";
        return strScript;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns <paramref name="clsElement"/> as an executable R script. </summary>
/// 
/// <param name="clsElement">   The R element to convert to an executable R script. The R element
///                             may have an associated package name, and a list of associated 
///                             objects e.g. 'pkg::obj1$obj2$fn1(a)'. </param>
/// 
/// <param name="bIncludeFormatting">   If True, then include all formatting information in 
///     returned string (comments, indents, padding spaces, extra line breaks etc.). </param>
/// 
/// <returns>   <paramref name="clsElement"/> as an executable R script. </returns>
/// --------------------------------------------------------------------------------------------
    private string GetScriptElementProperty(RElementProperty clsElement, bool bIncludeFormatting = true)
    {
        string strScript = (bIncludeFormatting ? clsElement.strPrefix : "") + (string.IsNullOrEmpty(clsElement.strPackageName) ? "" : clsElement.strPackageName + "::");
        if (!(clsElement.lstObjects == null) && clsElement.lstObjects.Count > 0)
        {
            foreach (var clsObject in clsElement.lstObjects)
            {
                strScript += GetScriptElement(clsObject, bIncludeFormatting);
                strScript += "$";
            }
        }
        strScript += clsElement.strTxt;
        return strScript;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   
/// Iterates through the tokens in <paramref name="lstTokens"/>, from position 
/// <paramref name="iPos"/> and makes each presentation element a child of the next 
/// non-presentation element. 
/// <para>
/// A presentation element is an element that has no functionality and is only used to make 
/// the script easier to read. It may be a block of spaces, a comment or a newline that does
/// not end a statement.
/// </para><para>
/// For example, the list of tokens representing the following block of script:
/// </para><code>
/// # comment1 <para>
/// a =b # comment2 </para></code><para>
/// </para><para>
/// Will be structured as:</para><code><para>
/// a</para><para>
/// .."# comment1\n"</para><para>
/// =</para><para>
/// .." "</para><para>
/// b</para><para>
/// (endStatement)</para><para>
/// .." # comment2"</para><para>
/// </para></code></summary>
/// 
/// <param name="lstTokens">   The list of tokens to process. </param>
/// <param name="iPos">        The position in the list to start processing </param>
/// 
/// <returns>   A token tree where presentation information is stored as a child of the next 
///             non-presentation element. </returns>
/// --------------------------------------------------------------------------------------------
    private static List<RToken> GetLstPresentation(List<RToken> lstTokens, int iPos)
    {

        if (lstTokens.Count < 1)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        string strPrefix = "";

        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];
            iPos += 1;
            switch (clsToken.tokentype)
            {
                case RToken.TokenTypes.RSpace:
                case RToken.TokenTypes.RComment:
                case RToken.TokenTypes.RNewLine:
                    {
                        strPrefix += clsToken.text;
                        break;
                    }

                default:
                    {
                        if (!string.IsNullOrEmpty(strPrefix))
                        {
                            clsToken.childTokens.Add(new RToken(strPrefix, RToken.TokenTypes.RPresentation));
                        }
                        lstTokensNew.Add(clsToken.CloneMe());
                        strPrefix = "";
                        break;
                    }
            }
        }

        // Edge case: if there is still presentation information not yet added to a tree element
        // (this may happen if the last statement in the script is not terminated 
        // with a new line or '}')
        if (!string.IsNullOrEmpty(strPrefix))
        {
            // add a new end statement token that contains the presentation information
            clsToken = new RToken("", RToken.TokenTypes.REndStatement);
            clsToken.childTokens.Add(new RToken(strPrefix, RToken.TokenTypes.RPresentation));
            lstTokensNew.Add(clsToken);
        }

        return lstTokensNew;
    }


    /// --------------------------------------------------------------------------------------------
/// <summary>   
/// Iterates through the tokens in <paramref name="lstTokens"/> from position 
/// <paramref name="iPos"/>. If the token is a '(' then it makes everything inside the brackets a 
/// child of the '(' token. If the '(' belongs to a function then makes the '(' a child of the 
/// function. Brackets may be nested. For example, '(a*(b+c))' is structured as:<code>
///   (<para>
///   ..a</para><para>
///   ..*</para><para>
///   ..(</para><para>
///   ....b</para><para>
///   ....+</para><para>
///   ....c</para><para>
///   ....)</para><para>
///   ..)</para></code></summary>
/// 
/// <param name="lstTokens">   The token tree to restructure. </param>
/// <param name="iPos">        [in,out] The position in the list to start processing </param>
/// 
/// <returns>   A token tree restructured for round brackets. </returns>
/// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenBrackets(List<RToken> lstTokens, ref int iPos)
    {

        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];
            iPos += 1;
            switch (clsToken.text ?? "")
            {
                case "(":
                    {
                        var lstTokensTmp = GetLstTokenBrackets(lstTokens, ref iPos);
                        foreach (RToken clsTokenChild in lstTokensTmp)
                        {
                            if (clsTokenChild == null)
                            {
                                throw new Exception("Token has illegal empty child.");
                            }
                            clsToken.childTokens.Add(clsTokenChild.CloneMe());
                        }

                        break;
                    }
                case ")":
                    {
                        lstTokensNew.Add(clsToken.CloneMe());
                        return lstTokensNew;
                    }
            }
            lstTokensNew.Add(clsToken.CloneMe());
        }
        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>
/// Traverses the tree of tokens in <paramref name="lstTokens"/>. If the token is a function name then it 
/// makes the subsequent '(' a child of the function name token. </summary>
/// 
/// <param name="lstTokens">   The token tree to restructure. </param>
/// 
/// <returns>   A token tree restructured for function names. </returns>
/// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenFunctionBrackets(List<RToken> lstTokens)
    {

        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        int iPos = 0;
        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];

            if (clsToken.tokentype == RToken.TokenTypes.RFunctionName)
            {
                // if next steps will go out of bounds, then throw developer error
                if (iPos > lstTokens.Count - 2)
                {
                    throw new Exception("The function's parameters have an unexpected format and cannot be processed.");
                }
                // make the function's open bracket a child of the function name
                iPos += 1;
                clsToken.childTokens.Add(lstTokens[iPos].CloneMe());
            }
            clsToken.childTokens = GetLstTokenFunctionBrackets(clsToken.CloneMe().childTokens);
            lstTokensNew.Add(clsToken.CloneMe());
            iPos += 1;
        }
        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>
/// Traverses the tree of tokens in <paramref name="lstTokens"/>. If the token is a ',' that 
/// separates function parameters, then it makes everything up to the next ',' or ')' a child 
/// of the ',' token. Parameters between function commas are optional. For example, 
/// `myFunction(a,,b)` is structured as: <code>
///   myFunction<para>
///   ..(</para><para>
///   ....a</para><para>
///   ....,</para><para>
///   ....,</para><para>
///   ......b</para><para>
///   ......)</para></code>
/// Commas used within square brackets (e.g. `a[b,c]`, `a[b,]` etc.) are ignored.
/// </summary>
/// 
/// <param name="lstTokens">        The token tree to restructure. </param>
/// <param name="iPos">             [in,out] The position in the list to start processing </param>
/// <param name="bProcessingComma"> (Optional) True if function called when already processing 
///     a comma (prevents commas being nested inside each other). </param>
/// 
/// <returns>   A token tree restructured for function commas. </returns>
/// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenFunctionCommas(List<RToken> lstTokens, ref int iPos, bool bProcessingComma = false)
    {
        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        var lstOpenBrackets = new List<string>() { "[", "[[" };
        var lstCloseBrackets = new List<string>() { "]", "]]" };
        int iNumOpenBrackets = 0;

        while (iPos < lstTokens.Count)
        {
            clsToken = lstTokens[iPos];

            // only process commas that separate function parameters,
            // ignore commas inside square bracket (e.g. `a[b,c]`)
            iNumOpenBrackets += lstOpenBrackets.Contains(clsToken.text) ? 1 : 0;
            iNumOpenBrackets -= lstCloseBrackets.Contains(clsToken.text) ? 1 : 0;
            if (iNumOpenBrackets == 0 && clsToken.text == ",")
            {
                if (bProcessingComma)
                {
                    iPos -= 1;  // ensure this comma is processed in the level above
                    return lstTokensNew;
                }
                else
                {
                    iPos += 1;
                    clsToken.childTokens = clsToken.childTokens.Concat(GetLstTokenFunctionCommas(lstTokens, ref iPos, true)).ToList();
                }
            }
            else
            {
                int argiPos = 0;
                clsToken.childTokens = GetLstTokenFunctionCommas(clsToken.CloneMe().childTokens, ref argiPos);
            }

            lstTokensNew.Add(clsToken);
            iPos += 1;
        }
        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary> 
/// Iterates through all the possible operators in order of precedence. For each operator, 
/// traverses the tree of tokens in <paramref name="lstTokens"/>. If the operator is found then 
/// the operator's parameters (typically the tokens to the left and right of the operator) are 
/// made children of the operator. For example, 'a*b+c' is structured as:<code>
///   +<para>
///   ..*</para><para>
///   ....a</para><para>
///   ....b</para><para>
///   ..c</para></code></summary>
/// 
/// <param name="lstTokens">   The token tree to restructure. </param>
/// 
/// <returns>   A token tree restructured for all the possible operators. </returns>
/// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenOperators(List<RToken> lstTokens)
    {
        if (lstTokens.Count <= 0)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        for (int iPosOperators = 0, loopTo = Information.UBound(operatorPrecedences) - 1; iPosOperators <= loopTo; iPosOperators++)
        {

            // restructure the tree for the next group of operators in the precedence list
            lstTokensNew = GetLstTokenOperatorGroup(lstTokens, iPosOperators);

            // clone the new tree before restructuring for the next operator
            lstTokens = new List<RToken>();
            foreach (RToken clsTokenTmp in lstTokensNew)
                lstTokens.Add(clsTokenTmp.CloneMe());
        }

        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>
/// Traverses the tree of tokens in <paramref name="lstTokens"/>. If one of the operators in 
/// the <paramref name="iPosOperators"/> group is found, then the operator's parameters 
/// (typically the tokens to the left and right of the operator) are made children of the 
/// operator. For example, 'a*b+c' is structured as:<code>
///   +<para>
///   ..*</para><para>
///   ....a</para><para>
///   ....b</para><para>
///   ..c</para></code>
/// 
/// Edge case: This function cannot process the  case where a binary operator is immediately 
/// followed by a unary operator with the same or a lower precedence (e.g. 'a^-b', 'a+~b', 
/// 'a~~b' etc.). This is because of the R default precedence rules. The workaround is to 
/// enclose the unary operator in brackets (e.g. 'a^(-b)', 'a+(~b)', 'a~(~b)' etc.).
/// </summary>
/// <param name="lstTokens">        The token tree to restructure. </param>
/// <param name="iPosOperators">  The group of operators to search for in the tree. </param>
/// 
/// <returns>   A token tree restructured for the specified group of operators. </returns>
/// --------------------------------------------------------------------------------------------
    private List<RToken> GetLstTokenOperatorGroup(List<RToken> lstTokens, int iPosOperators)
    {

        if (lstTokens.Count < 1)
        {
            return new List<RToken>();
        }

        var lstTokensNew = new List<RToken>();
        RToken clsToken;
        RToken? clsTokenPrev = null;
        bool bPrevTokenProcessed = false;

        int iPosTokens = 0;
        while (iPosTokens < lstTokens.Count)
        {
            clsToken = lstTokens[iPosTokens].CloneMe();

            // if the token is the operator we are looking for and it has not been processed already
            // Edge case: if the operator already has (non-presentation) children then it means 
            // that it has already been processed. This happens when the child is in the 
            // same precedence group as the parent but was processed first in accordance 
            // with the left to right rule (e.g. 'a/b*c').
            if ((operatorPrecedences[iPosOperators].Contains(clsToken.text) || iPosOperators == iOperatorsUserDefined && Regex.IsMatch(clsToken.text, "^%.*%$")) && (clsToken.childTokens.Count == 0 || clsToken.childTokens.Count == 1 && clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation))
            {

                switch (clsToken.tokentype)
                {
                    case RToken.TokenTypes.ROperatorBracket: // handles '[' and '[['
                        {
                            if (iPosOperators != iOperatorsBrackets)
                            {
                                break;
                            }

                            // make the previous and next tokens (up to the corresponding close bracket), the children of the current token
                            if (clsTokenPrev == null)
                            {
                                throw new Exception("The bracket operator has no parameter on its left.");
                            }
                            clsToken.childTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
                            iPosTokens += 1;
                            string strCloseBracket = clsToken.text == "[" ? "]" : "]]";
                            int iNumOpenBrackets = 1;
                            while (iPosTokens < lstTokens.Count)
                            {
                                iNumOpenBrackets += (lstTokens[iPosTokens].text ?? "") == (clsToken.text ?? "") ? 1 : 0;
                                iNumOpenBrackets -= (lstTokens[iPosTokens].text ?? "") == (strCloseBracket ?? "") ? 1 : 0;
                                // discard the terminating cloe bracket
                                if (iNumOpenBrackets == 0)
                                {
                                    break;
                                }
                                clsToken.childTokens.Add(lstTokens[iPosTokens].CloneMe());
                                iPosTokens += 1;
                            }

                            break;
                        }

                    case RToken.TokenTypes.ROperatorBinary:
                        {
                            // edge case: if we are looking for unary '+' or '-' and we found a binary '+' or '-'
                            if (iPosOperators == iOperatorsUnaryOnly)
                            {
                                // do not process (binary '+' and '-' have a lower precedence and will be processed later)
                                break;
                            }
                            else if (clsTokenPrev == null)
                            {
                                throw new Exception("The binary operator has no parameter on its left.");
                            }

                            // make the previous and next tokens, the children of the current token
                            clsToken.childTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
                            clsToken.childTokens.Add(GetNextToken(lstTokens, iPosTokens));
                            iPosTokens += 1;
                            // while next token is the same operator (e.g. 'a+b+c+d...'), 
                            // then keep making the next token, the child of the current operator token
                            RToken clsTokenNext;
                            while (iPosTokens < lstTokens.Count - 1)
                            {
                                clsTokenNext = GetNextToken(lstTokens, iPosTokens);
                                if (!(clsToken.tokentype == clsTokenNext.tokentype) || !((clsToken.text ?? "") == (clsTokenNext.text ?? "")))
                                {
                                    break;
                                }

                                iPosTokens += 1;
                                clsToken.childTokens.Add(GetNextToken(lstTokens, iPosTokens));
                                iPosTokens += 1;
                            }

                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryRight:
                        {
                            // edge case: if we found a unary '+' or '-', but we are not currently processing the unary '+'and '-' operators
                            if (operatorPrecedences[iOperatorsUnaryOnly].Contains(clsToken.text) && !(iPosOperators == iOperatorsUnaryOnly))
                            {
                                break;
                            }
                            // make the next token, the child of the current operator token
                            clsToken.childTokens.Add(GetNextToken(lstTokens, iPosTokens));
                            iPosTokens += 1;
                            break;
                        }
                    case RToken.TokenTypes.ROperatorUnaryLeft:
                        {
                            if (clsTokenPrev == null || !(iPosOperators == iOperatorsTilda))
                            {
                                throw new Exception("Illegal unary left operator ('~' is the only valid unary left operator).");
                            }
                            // make the previous token, the child of the current operator token
                            clsToken.childTokens.Add(clsTokenPrev.CloneMe());
                            bPrevTokenProcessed = true;
                            break;
                        }

                    default:
                        {
                            throw new Exception("The token has an unknown operator type.");
                        }
                }
            }

            // if token was not the operator we were looking for
            // (or we were looking for a unary right operator)
            if (!bPrevTokenProcessed && !(clsTokenPrev == null))
            {
                // add the previous token to the tree
                lstTokensNew.Add(clsTokenPrev);
            }

            // process the current token's children
            clsToken.childTokens = GetLstTokenOperatorGroup(clsToken.CloneMe().childTokens, iPosOperators);

            clsTokenPrev = clsToken.CloneMe();
            bPrevTokenProcessed = false;
            iPosTokens += 1;
        }

        if (clsTokenPrev == null)
        {
            throw new Exception("Expected that there would still be a token to add to the tree.");
        }
        lstTokensNew.Add(clsTokenPrev.CloneMe());

        return lstTokensNew;
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns a clone of the next token in the <paramref name="lstTokens"/> list, 
///             after <paramref name="iPosTokens"/>. If there is no next token then throws 
///             an error.</summary>
/// 
/// <param name="lstTokens">      The list of tokens. </param>
/// <param name="iPosTokens">     The position of the current token in the list. </param>
/// 
/// <returns>   A clone of the next token in the <paramref name="lstTokens"/> list. </returns>
/// --------------------------------------------------------------------------------------------
    private static RToken GetNextToken(List<RToken> lstTokens, int iPosTokens)
    {
        if (iPosTokens >= lstTokens.Count - 1)
        {
            throw new Exception("Token list ended unexpectedly.");
        }
        return lstTokens[iPosTokens + 1].CloneMe();
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns an R element object constructed from the <paramref name="clsToken"/> 
///             token. </summary>
/// 
/// <param name="clsToken">         The token to convert into an R element object. </param>
/// <param name="dctAssignments">   Dictionary containing all the current existing assignments. 
///                                 The key is the name of the variable. The value is a reference 
///                                 to the R statement that performed the assignment. </param>
/// <param name="bBracketedNew">    (Optional) True if the token is enclosed in brackets. </param>
/// <param name="strPackageName">   (Optional) The package name associated with the token. </param>
/// <param name="strPackagePrefix"> (Optional) The formatting string that prefixes the package 
///                                 name (e.g. spaces or comment lines). </param>
/// <param name="lstObjects">       (Optional) The list of objects associated with the token 
///                                 (e.g. 'obj1$obj2$myFn()'). </param>
/// 
/// <returns>   An R element object constructed from the <paramref name="clsToken"/>
///             token. </returns>
/// --------------------------------------------------------------------------------------------
    private RElement? GetRElement(RToken clsToken, Dictionary<string, RStatement> dctAssignments, bool bBracketedNew = false, string strPackageName = "", string strPackagePrefix = "", List<RElement>? lstObjects = null)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot create an R element from an empty token.");
        }

        switch (clsToken.tokentype)
        {
            case RToken.TokenTypes.RBracket:
                {
                    // if text is a round bracket, then return the bracket's child
                    if (clsToken.text == "(")
                    {
                        // an open bracket must have at least one child
                        if (clsToken.childTokens.Count < 1 || clsToken.childTokens.Count > 3)
                        {
                            throw new Exception("Open bracket token has " + clsToken.childTokens.Count + " children. An open bracket must have exactly one child (plus an " + "optional presentation child and/or an optional close bracket).");
                        }
                        return GetRElement(GetChildPosNonPresentation(clsToken), dctAssignments, true);
                    }
                    return new RElement(clsToken);
                }

            case RToken.TokenTypes.RFunctionName:
                {
                    var clsFunction = new RElementFunction(clsToken, bBracketedNew, strPackageName, strPackagePrefix, lstObjects);
                    // Note: Function tokens are structured as a tree.
                    // For example 'f(a,b,c=d)' is structured as:
                    // f
                    // ..(
                    // ....a
                    // ....,
                    // ......b 
                    // ....,
                    // ......=
                    // ........c
                    // ........d
                    // ........)    
                    // 
                    if (clsToken.childTokens.Count < 1 || clsToken.childTokens.Count > 2)
                    {
                        throw new Exception("Function token has " + clsToken.childTokens.Count + " children. A function token must have 1 child (plus an optional presentation child).");
                    }

                    // process each parameter
                    bool bFirstParam = true;
                    foreach (var clsTokenParam in clsToken.childTokens[clsToken.childTokens.Count - 1].childTokens)
                    {
                        // if list item is a presentation element, then ignore it
                        if (clsTokenParam.tokentype == RToken.TokenTypes.RPresentation)
                        {
                            if (bFirstParam)
                            {
                                continue;
                            }
                            throw new Exception("Function parameter list contained an unexpected presentation element.");
                        }

                        var clsParameter = GetRParameterNamed(clsTokenParam, dctAssignments);
                        if (!(clsParameter == null))
                        {
                            if (bFirstParam && clsParameter.clsArgValue == null)
                            {
                                clsFunction.lstParameters.Add(clsParameter); // add extra empty parameter for case 'f(,)'
                            }
                            clsFunction.lstParameters.Add(clsParameter);
                        }
                        bFirstParam = false;
                    }
                    return clsFunction;
                }

            case RToken.TokenTypes.ROperatorUnaryLeft:
                {
                    if (clsToken.childTokens.Count < 1 || clsToken.childTokens.Count > 2)
                    {
                        throw new Exception("Unary left operator token has " + clsToken.childTokens.Count + " children. A Unary left operator must have 1 child (plus an optional presentation child).");
                    }
                    var clsOperator = new RElementOperator(clsToken, bBracketedNew);
                    clsOperator.lstParameters.Add(GetRParameter(clsToken.childTokens[clsToken.childTokens.Count - 1], dctAssignments));
                    return clsOperator;
                }

            case RToken.TokenTypes.ROperatorUnaryRight:
                {
                    if (clsToken.childTokens.Count < 1 || clsToken.childTokens.Count > 2)
                    {
                        throw new Exception("Unary right operator token has " + clsToken.childTokens.Count + " children. A Unary right operator must have 1 child (plus an optional presentation child).");
                    }
                    var clsOperator = new RElementOperator(clsToken, bBracketedNew, true);
                    clsOperator.lstParameters.Add(GetRParameter(clsToken.childTokens[clsToken.childTokens.Count - 1], dctAssignments));
                    return clsOperator;
                }

            case RToken.TokenTypes.ROperatorBinary:
                {
                    if (clsToken.childTokens.Count < 2)
                    {
                        throw new Exception("Binary operator token has " + clsToken.childTokens.Count + " children. A binary operator must have at least 2 children (plus an optional presentation child).");
                    }

                    // if object operator
                    switch (clsToken.text ?? "")
                    {
                        case "$":
                            {
                                string strPackagePrefixNew = "";
                                string strPackageNameNew = "";
                                var lstObjectsNew = new List<RElement>();

                                // add each object parameter to the object list (except last parameter)
                                int startPos = clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? 1 : 0;
                                for (int iPos = startPos, loopTo = clsToken.childTokens.Count - 2; iPos <= loopTo; iPos++)
                                {
                                    var clsTokenObject = clsToken.childTokens[iPos];
                                    // if the first parameter is a package operator ('::'), then make this the package name for the returned element
                                    if (iPos == startPos && clsTokenObject.tokentype == RToken.TokenTypes.ROperatorBinary && clsTokenObject.text == "::")
                                    {
                                        // get the package name and any package presentation information
                                        strPackageNameNew = GetTokenPackageName(clsTokenObject).text;
                                        strPackagePrefixNew = GetPackagePrefix(clsTokenObject);
                                        // get the object associated with the package, and add it to the object list
                                        var objectElement = GetRElement(clsTokenObject.childTokens[clsTokenObject.childTokens.Count - 1], dctAssignments) ?? throw new Exception("The package operator '::' has no associated element.");
                                        lstObjectsNew.Add(objectElement);
                                        continue;
                                    }
                                    var element = GetRElement(clsTokenObject, dctAssignments) ?? throw new Exception("The object operator '$' has no associated element.");
                                    lstObjectsNew.Add(element);
                                }
                                // the last item in the parameter list is the element we need to return
                                return GetRElement(clsToken.childTokens[clsToken.childTokens.Count - 1], dctAssignments, bBracketedNew, strPackageNameNew, strPackagePrefixNew, lstObjectsNew);
                            }

                        case "::":
                            {
                                // the '::' operator parameter list contains:
                                // - the presentation string (optional)
                                // - the package name
                                // - the element associated with the package
                                return GetRElement(clsToken.childTokens[clsToken.childTokens.Count - 1], dctAssignments, bBracketedNew, GetTokenPackageName(clsToken).text, GetPackagePrefix(clsToken)); // else if not an object or package operator, then add each parameter to the operator
                            }

                        default:
                            {
                                var clsOperator = new RElementOperator(clsToken, bBracketedNew);
                                int startPos = clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? 1 : 0;
                                for (int iPos = startPos, loopTo1 = clsToken.childTokens.Count - 1; iPos <= loopTo1; iPos++)
                                    clsOperator.lstParameters.Add(GetRParameter(clsToken.childTokens[iPos], dctAssignments));
                                return clsOperator;
                            }
                    }
                }

            case RToken.TokenTypes.ROperatorBracket:
                {
                    if (clsToken.childTokens.Count < 1)
                    {
                        throw new Exception("Square bracket operator token has no children. A binary " + "operator must have at least 1 child (plus an optional " + "presentation child).");

                    }

                    var clsBracketOperator = new RElementOperator(clsToken, bBracketedNew);
                    int startPos = clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? 1 : 0;
                    for (int iPos = startPos, loopTo2 = clsToken.childTokens.Count - 1; iPos <= loopTo2; iPos++)
                        clsBracketOperator.lstParameters.Add(GetRParameter(clsToken.childTokens[iPos], dctAssignments));
                    return clsBracketOperator;
                }

            case RToken.TokenTypes.RSyntacticName:
            case RToken.TokenTypes.RConstantString:
                {
                    // if element has a package name or object list, then return a property element
                    if (!string.IsNullOrEmpty(strPackageName) || !(lstObjects == null))
                    {
                        return new RElementProperty(clsToken, lstObjects, bBracketedNew, strPackageName, strPackagePrefix);
                    }

                    // if element was assigned in a previous statement, then return an assigned element
                    var clsStatement = dctAssignments.ContainsKey(clsToken.text) ? dctAssignments[clsToken.text] : null;
                    if (!(clsStatement == null))
                    {
                        return new RElementAssignable(clsToken, clsStatement, bBracketedNew);
                    }

                    // else just return a regular element
                    return new RElement(clsToken, bBracketedNew);
                }

            case RToken.TokenTypes.RSeparator: // a comma within a square bracket, e.g. `a[b,c]`
                {
                    // just return a regular element
                    return new RElement(clsToken, bBracketedNew);
                }

            case RToken.TokenTypes.REndStatement:
                {
                    return null;
                }

            default:
                {
                    throw new Exception("The token has an unexpected type.");
                }
        }

        throw new Exception("It should be impossible for the code to reach this point.");
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns the package name token associated with the <paramref name="clsToken"/> 
///             package operator. </summary>
/// 
/// <param name="clsToken"> Package operator ('::') token. </param>
/// 
/// <returns>   The package name associated with the <paramref name="clsToken"/> package 
///             operator. </returns>
/// --------------------------------------------------------------------------------------------
    private static RToken GetTokenPackageName(RToken clsToken)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot return a package name from an empty token.");
        }

        if (clsToken.childTokens.Count < 2 || clsToken.childTokens.Count > 3)
        {
            throw new Exception("The package operator '::' has " + clsToken.childTokens.Count + " parameters. It must have 2 parameters (plus an optional presentation parameter).");
        }
        return clsToken.childTokens[clsToken.childTokens.Count - 2];
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns the formatting prefix (spaces or comment lines) associated with the 
///             <paramref name="clsToken"/> package operator. If the package operator has no 
///             associated formatting, then returns an empty string.</summary>
/// 
/// <param name="clsToken"> Package operator ('::') token. </param>
/// 
/// <returns>   The formatting prefix (spaces or comment lines) associated with the
///             <paramref name="clsToken"/> package operator. </returns>
/// --------------------------------------------------------------------------------------------
    private string GetPackagePrefix(RToken clsToken)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot return a package prefix from an empty token.");
        }

        var clsTokenPackageName = GetTokenPackageName(clsToken);
        return clsTokenPackageName.childTokens.Count > 0 && clsTokenPackageName.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? clsTokenPackageName.childTokens[0].text : "";
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   
/// Returns a named parameter element constructed from the <paramref name="clsToken"/> token 
/// tree. The top-level element in the token tree may be:<list type="bullet"><item>
/// 'value' e.g. for fn(a)</item><item>
/// '=' e.g. for 'fn(a=1)'</item><item>
/// ',' e.g. for 'fn(a,b) or 'fn(a=1,b,,c,)'</item><item>
/// ')' indicates the end of the parameter list, returns nothing</item>
/// </list></summary>
/// 
/// <param name="clsToken">         The token tree to convert into a named parameter element. </param>
/// <param name="dctAssignments">   Dictionary containing all the current existing assignments.
///                                 The key is the name of the variable. The value is a reference
///                                 to the R statement that performed the assignment. </param>
/// 
/// <returns>   A named parameter element constructed from the <paramref name="clsToken"/> token
///             tree. </returns>
/// --------------------------------------------------------------------------------------------
    private RParameter? GetRParameterNamed(RToken clsToken, Dictionary<string, RStatement> dctAssignments)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot create a named parameter from an empty token.");
        }

        switch (clsToken.text ?? "")
        {
            case "=":
                {
                    if (clsToken.childTokens.Count < 2)
                    {
                        throw new Exception("Named parameter token has " + clsToken.childTokens.Count + " children. Named parameter must have at least 2 children (plus an optional presentation child).");
                    }

                    var clsTokenArgumentName = clsToken.childTokens[clsToken.childTokens.Count - 2];
                    var clsParameter = new RParameter() { strArgName = clsTokenArgumentName.text };
                    clsParameter.clsArgValue = GetRElement(clsToken.childTokens[clsToken.childTokens.Count - 1], dctAssignments);

                    // set the parameter's formatting prefix to the prefix of the parameter name
                    // Note: if the equals sign has any formatting information then this information 
                    // will be lost.
                    clsParameter.strPrefix = clsTokenArgumentName.childTokens.Count > 0 && clsTokenArgumentName.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? clsTokenArgumentName.childTokens[0].text : "";

                    return clsParameter;
                }
            case ",":
                {
                    // if ',' is followed by a parameter name or value (e.g. 'fn(a,b)'), then return the parameter
                    try
                    {
                        // throws exception if nonpresentation child not found
                        return GetRParameterNamed(GetChildPosNonPresentation(clsToken), dctAssignments);
                    }
                    catch (Exception)
                    {
                        // return empty parameter (e.g. for cases like 'fn(a,)')
                        return new RParameter();
                    }
                }
            case ")":
                {
                    return null;
                }

            default:
                {
                    var clsParameterNamed = new RParameter() { clsArgValue = GetRElement(clsToken, dctAssignments) };
                    clsParameterNamed.strPrefix = clsToken.childTokens.Count > 0 && clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? clsToken.childTokens[0].text : "";
                    return clsParameterNamed;
                }
        }
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns the first child of <paramref name="clsToken"/> that is not a 
///             presentation token or a close bracket ')'. </summary>
/// 
/// <param name="clsToken"> The token tree to search for non-presentation children. </param>
/// 
/// <returns>   The first child of <paramref name="clsToken"/> that is not a presentation token 
///             or a close bracket ')'. </returns>
/// --------------------------------------------------------------------------------------------
    private static RToken GetChildPosNonPresentation(RToken clsToken)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot return a non-presentation child from an empty token.");
        }

        // for each child token
        foreach (var clsTokenChild in clsToken.childTokens)
        {
            // if token is not a presentation token or a close bracket ')', then return the token
            if (!(clsTokenChild.tokentype == RToken.TokenTypes.RPresentation) && !(clsTokenChild.tokentype == RToken.TokenTypes.RBracket && clsTokenChild.text == ")"))
            {
                return clsTokenChild;
            }
        }
        throw new Exception("Token must contain at least one non-presentation child.");
    }

    /// --------------------------------------------------------------------------------------------
/// <summary>   Returns a  parameter element constructed from the <paramref name="clsToken"/> 
///             token tree. </summary>
/// 
/// <param name="clsToken">         The token tree to convert into a parameter element. </param>
/// <param name="dctAssignments">   Dictionary containing all the current existing assignments.
///                                 The key is the name of the variable. The value is a reference
///                                 to the R statement that performed the assignment. </param>
/// 
/// <returns>   A parameter element constructed from the <paramref name="clsToken"/> token tree. </returns>
/// --------------------------------------------------------------------------------------------
    private RParameter GetRParameter(RToken clsToken, Dictionary<string, RStatement> dctAssignments)
    {
        if (clsToken == null)
        {
            throw new ArgumentException("Cannot create a parameter from an empty token.");
        }
        return new RParameter()
        {
            clsArgValue = GetRElement(clsToken, dctAssignments),
            strPrefix = clsToken.childTokens.Count > 0 && clsToken.childTokens[0].tokentype == RToken.TokenTypes.RPresentation ? clsToken.childTokens[0].text : ""
        };
    }

}