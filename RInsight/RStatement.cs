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


    /// --------------------------------------------------------------------------------------------
    /// <summary>   
    /// Constructs an object representing a valid R statement.<para>
    /// Processes the tokens from <paramref name="token"/> from position <paramref name="iPos"/> 
    /// to the end of statement, end of script or end of list (whichever comes first).</para></summary>
    /// 
    /// <param name="token">   The list of R tokens to process </param>
    /// <param name="iPos">      [in,out] The position in the list to start processing </param>
    /// <param name="dctAssignments">   A dictionary of assignments in the parent script.</param>
    /// --------------------------------------------------------------------------------------------
    public RStatement(RToken token, RToken tokenEndStatement, Dictionary<string, RStatement> dctAssignments)
    {

        // if the statement includes an assignment, then construct the assignment element
        if (token.TokenType == RToken.TokenTypes.ROperatorBinary && token.ChildTokens.Count > 1)
        {

            var clsTokenChildLeft = token.ChildTokens[token.ChildTokens.Count - 2];
            var clsTokenChildRight = token.ChildTokens[token.ChildTokens.Count - 1];

            // if the statement has a left assignment (e.g. 'x<-value', 'x<<-value' or 'x=value')
            if (new string[] { "<-", "<<-" }.Contains(token.Lexeme.Text) 
                || new string[] { "=" }.Contains(token.Lexeme.Text))
            {
                clsAssignment = GetRElement(clsTokenChildLeft, dctAssignments);
                clsElement = GetRElement(clsTokenChildRight, dctAssignments);
            }
            else if (new string[] { "->", "->>" }.Contains(token.Lexeme.Text))
            {
                // else if the statement has a right assignment (e.g. 'value->x' or 'value->>x')
                clsAssignment = GetRElement(clsTokenChildRight, dctAssignments);
                clsElement = GetRElement(clsTokenChildLeft, dctAssignments);
            }
        }

        // if there was an assigment then set the assignment operator and its presentation information
        if (!(clsAssignment == null))
        {
            strAssignmentOperator = token.Lexeme.Text;
            strAssignmentPrefix = token.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? token.ChildTokens[0].Lexeme.Text : "";
        }
        else // if there was no assignment, then build the main element from the token tree's top element
        {
            clsElement = GetRElement(token, dctAssignments);
        }

        // if statement ends with a semicolon or newline
        //todo var clsTokenEndStatement = lstTokenTree[lstTokenTree.Count - 1];
        //todo if (clsTokenEndStatement.Tokentype == RToken.TokenType.REndStatement || clsTokenEndStatement.Tokentype == RToken.TokenType.REndScript)
        if (tokenEndStatement.TokenType == RToken.TokenTypes.REndStatement)
        {
            if (tokenEndStatement.Lexeme.Text == ";")
            {
                bTerminateWithNewline = false;
            }
            else // store any remaining presentation data associated with the newline
            {
                strSuffix = tokenEndStatement.ChildTokens.Count > 0 && tokenEndStatement.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? tokenEndStatement.ChildTokens[0].Lexeme.Text : "";
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
            if (new string[] { "<-", "<<-" }.Contains(strAssignmentOperator) 
                || new string[] { "=" }.Contains(strAssignmentOperator))
            {
                strScript = strAssignment + strAssignmentPrefixTmp + strAssignmentOperator + strElement;
            }
            else if (new string[] { "->", "->>" }.Contains(strAssignmentOperator))
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

        switch (clsToken.TokenType)
        {
            case RToken.TokenTypes.RBracket:
                {
                    // if text is a round bracket, then return the bracket's child
                    if (clsToken.Lexeme.Text == "(")
                    {
                        // an open bracket must have at least one child
                        if (clsToken.ChildTokens.Count < 1 || clsToken.ChildTokens.Count > 3)
                        {
                            throw new Exception("Open bracket token has " + clsToken.ChildTokens.Count + " children. An open bracket must have exactly one child (plus an " + "optional presentation child and/or an optional close bracket).");
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
                    if (clsToken.ChildTokens.Count < 1 || clsToken.ChildTokens.Count > 2)
                    {
                        throw new Exception("Function token has " + clsToken.ChildTokens.Count + " children. A function token must have 1 child (plus an optional presentation child).");
                    }

                    // process each parameter
                    bool bFirstParam = true;
                    foreach (var clsTokenParam in clsToken.ChildTokens[clsToken.ChildTokens.Count - 1].ChildTokens)
                    {
                        // if list item is a presentation element, then ignore it
                        if (clsTokenParam.TokenType == RToken.TokenTypes.RPresentation)
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
                    if (clsToken.ChildTokens.Count < 1 || clsToken.ChildTokens.Count > 2)
                    {
                        throw new Exception("Unary left operator token has " + clsToken.ChildTokens.Count + " children. A Unary left operator must have 1 child (plus an optional presentation child).");
                    }
                    var clsOperator = new RElementOperator(clsToken, bBracketedNew);
                    clsOperator.lstParameters.Add(GetRParameter(clsToken.ChildTokens[clsToken.ChildTokens.Count - 1], dctAssignments));
                    return clsOperator;
                }

            case RToken.TokenTypes.ROperatorUnaryRight:
                {
                    if (clsToken.ChildTokens.Count < 1 || clsToken.ChildTokens.Count > 2)
                    {
                        throw new Exception("Unary right operator token has " + clsToken.ChildTokens.Count + " children. A Unary right operator must have 1 child (plus an optional presentation child).");
                    }
                    var clsOperator = new RElementOperator(clsToken, bBracketedNew, true);
                    clsOperator.lstParameters.Add(GetRParameter(clsToken.ChildTokens[clsToken.ChildTokens.Count - 1], dctAssignments));
                    return clsOperator;
                }

            case RToken.TokenTypes.ROperatorBinary:
                {
                    if (clsToken.ChildTokens.Count < 2)
                    {
                        throw new Exception("Binary operator token has " + clsToken.ChildTokens.Count + " children. A binary operator must have at least 2 children (plus an optional presentation child).");
                    }

                    // if object operator
                    switch (clsToken.Lexeme.Text ?? "")
                    {
                        case "$":
                            {
                                string strPackagePrefixNew = "";
                                string strPackageNameNew = "";
                                var lstObjectsNew = new List<RElement>();

                                // add each object parameter to the object list (except last parameter)
                                int startPos = clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? 1 : 0;
                                for (int iPos = startPos, loopTo = clsToken.ChildTokens.Count - 2; iPos <= loopTo; iPos++)
                                {
                                    var clsTokenObject = clsToken.ChildTokens[iPos];
                                    // if the first parameter is a package operator ('::'), then make this the package name for the returned element
                                    if (iPos == startPos && clsTokenObject.TokenType == RToken.TokenTypes.ROperatorBinary && clsTokenObject.Lexeme.Text == "::")
                                    {
                                        // get the package name and any package presentation information
                                        strPackageNameNew = GetTokenPackageName(clsTokenObject).Lexeme.Text;
                                        strPackagePrefixNew = GetPackagePrefix(clsTokenObject);
                                        // get the object associated with the package, and add it to the object list
                                        var objectElement = GetRElement(clsTokenObject.ChildTokens[clsTokenObject.ChildTokens.Count - 1], dctAssignments) ?? throw new Exception("The package operator '::' has no associated element.");
                                        lstObjectsNew.Add(objectElement);
                                        continue;
                                    }
                                    var element = GetRElement(clsTokenObject, dctAssignments) ?? throw new Exception("The object operator '$' has no associated element.");
                                    lstObjectsNew.Add(element);
                                }
                                // the last item in the parameter list is the element we need to return
                                return GetRElement(clsToken.ChildTokens[clsToken.ChildTokens.Count - 1], dctAssignments, bBracketedNew, strPackageNameNew, strPackagePrefixNew, lstObjectsNew);
                            }

                        case "::":
                            {
                                // the '::' operator parameter list contains:
                                // - the presentation string (optional)
                                // - the package name
                                // - the element associated with the package
                                return GetRElement(clsToken.ChildTokens[clsToken.ChildTokens.Count - 1], dctAssignments, bBracketedNew, GetTokenPackageName(clsToken).Lexeme.Text, GetPackagePrefix(clsToken)); // else if not an object or package operator, then add each parameter to the operator
                            }

                        default:
                            {
                                var clsOperator = new RElementOperator(clsToken, bBracketedNew);
                                int startPos = clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? 1 : 0;
                                for (int iPos = startPos, loopTo1 = clsToken.ChildTokens.Count - 1; iPos <= loopTo1; iPos++)
                                    clsOperator.lstParameters.Add(GetRParameter(clsToken.ChildTokens[iPos], dctAssignments));
                                return clsOperator;
                            }
                    }
                }

            case RToken.TokenTypes.ROperatorBracket:
                {
                    if (clsToken.ChildTokens.Count < 1)
                    {
                        throw new Exception("Square bracket operator token has no children. A binary " + "operator must have at least 1 child (plus an optional " + "presentation child).");

                    }

                    var clsBracketOperator = new RElementOperator(clsToken, bBracketedNew);
                    int startPos = clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? 1 : 0;
                    for (int iPos = startPos, loopTo2 = clsToken.ChildTokens.Count - 1; iPos <= loopTo2; iPos++)
                        clsBracketOperator.lstParameters.Add(GetRParameter(clsToken.ChildTokens[iPos], dctAssignments));
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
                    var clsStatement = dctAssignments.ContainsKey(clsToken.Lexeme.Text) ? dctAssignments[clsToken.Lexeme.Text] : null;
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
            case RToken.TokenTypes.RInvalid:
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

        if (clsToken.ChildTokens.Count < 2 || clsToken.ChildTokens.Count > 3)
        {
            throw new Exception("The package operator '::' has " + clsToken.ChildTokens.Count + " parameters. It must have 2 parameters (plus an optional presentation parameter).");
        }
        return clsToken.ChildTokens[clsToken.ChildTokens.Count - 2];
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
        return clsTokenPackageName.ChildTokens.Count > 0 && clsTokenPackageName.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? clsTokenPackageName.ChildTokens[0].Lexeme.Text : "";
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

        switch (clsToken.Lexeme.Text ?? "")
        {
            case "=":
                {
                    if (clsToken.ChildTokens.Count < 2)
                    {
                        throw new Exception("Named parameter token has " + clsToken.ChildTokens.Count + " children. Named parameter must have at least 2 children (plus an optional presentation child).");
                    }

                    var clsTokenArgumentName = clsToken.ChildTokens[clsToken.ChildTokens.Count - 2];
                    var clsParameter = new RParameter() { strArgName = clsTokenArgumentName.Lexeme.Text };
                    clsParameter.clsArgValue = GetRElement(clsToken.ChildTokens[clsToken.ChildTokens.Count - 1], dctAssignments);

                    // set the parameter's formatting prefix to the prefix of the parameter name
                    // Note: if the equals sign has any formatting information then this information 
                    // will be lost.
                    clsParameter.strPrefix = clsTokenArgumentName.ChildTokens.Count > 0 && clsTokenArgumentName.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? clsTokenArgumentName.ChildTokens[0].Lexeme.Text : "";

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
                    clsParameterNamed.strPrefix = clsToken.ChildTokens.Count > 0 && clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? clsToken.ChildTokens[0].Lexeme.Text : "";
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
        foreach (var clsTokenChild in clsToken.ChildTokens)
        {
            // if token is not a presentation token or a close bracket ')', then return the token
            if (!(clsTokenChild.TokenType == RToken.TokenTypes.RPresentation) && !(clsTokenChild.TokenType == RToken.TokenTypes.RBracket && clsTokenChild.Lexeme.Text == ")"))
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
            strPrefix = clsToken.ChildTokens.Count > 0 && clsToken.ChildTokens[0].TokenType == RToken.TokenTypes.RPresentation ? clsToken.ChildTokens[0].Lexeme.Text : ""
        };
    }

}