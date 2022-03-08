// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.JavaScript;

/// <summary>
/// An object that represents a JavaScript method.
/// </summary>
/// <param name="JavaScriptApiMethodName">The exact name of the JavaScript API method.</param>
/// <param name="InvokableMethodName">
/// The invokable method name, when different than
/// the <paramref name="JavaScriptApiMethodName"/>
/// the method is not considered pure.</param>
/// <param name="ParameterDefinitions">The optional listing of method parameters.</param>
internal sealed record JavaScriptMethod(
    string JavaScriptApiMethodName,
    string? InvokableMethodName = null,
    List<CSharpType>? ParameterDefinitions = null)
{
    /// <summary>
    /// A "pure" JavaScript method is one that
    /// can be invoked without custom JavaScript.
    /// </summary>
    /// <remarks>
    /// The following example is a "pure" JavaScript function.
    /// <code type="javascript">
    /// window.prompt(message, default);
    /// </code>
    /// This can be called from an <c>IJSRuntime</c> or <c>IJSObjectReference</c> instance directly.
    /// </remarks>
    public bool IsPure =>
        JavaScriptApiMethodName == InvokableMethodName ||
        InvokableMethodName is null;
}
