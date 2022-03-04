// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Source;

static partial class SourceCode
{
    internal const string BlazorHostingModel = @"/// <summary>
/// The Blazor hosting model source, either WebAssembly or Server.
/// </summary>
public enum BlazorHostingModel
{
    /// <summary>
    /// This is the default. Use this to source generate targeting the synchronous <c>IJSInProcessRuntime</c> type.
    /// </summary>
    WebAssembly,

    /// <summary>
    /// Use this to source generate targeting the synchronous <c>IJSRuntime</c> type.
    /// </summary>
    Server
};
";
}