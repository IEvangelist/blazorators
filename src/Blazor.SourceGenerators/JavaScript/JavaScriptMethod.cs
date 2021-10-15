// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using TypeScript.TypeConverter.CSharp;

namespace TypeScript.TypeConverter.JavaScript
{
    public record JavaScriptMethod(
        /// <summary>
        /// The exact name of the JavaScript API method.
        /// </summary>
        string JavaScriptApiMethodName,

        /// <summary>
        /// The invokable method name, when different than
        /// the <paramref name="JavaScriptApiMethodName"/>
        /// the method is not considered pure.
        /// </summary>
        string InvokableMethodName,

        /// <summary>
        /// The optional listing of method parameters.
        /// </summary>
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
        public bool IsPure => JavaScriptApiMethodName != InvokableMethodName;
    }
}