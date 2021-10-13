// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using TypeScript.TypeConverter.CSharp;

namespace TypeScript.TypeConverter.JavaScript;

internal record JavaScriptMethod(

    string InvokableMethodName,

    string JavaScriptApiMethodName,

    /// <summary>
    /// A "pure" JavaScript method is one that
    /// can be invoked without custom JavaScript.
    /// </summary>
    /// <remarks>
    /// The following example is a "pure" JavaScript function.
    /// <code type="javascript">
    /// window.prompt('')
    /// </code>
    /// </remarks>
    bool IsPure,

    List<CSharpType> Parameters);
