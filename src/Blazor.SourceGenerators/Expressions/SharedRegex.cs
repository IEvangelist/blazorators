// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Expressions;

internal static class SharedRegex
{
    // See: https://regex101.com/r/GV3DiG/1
    public static readonly Regex InterfaceRegex =
        new("^(?'declaration'interface.*?{.*?})$",
            RegexOptions.Singleline | RegexOptions.Multiline);

    public static readonly Regex InterfaceTypeNameRegex =
        new("(?:interface )(?'TypeName'\\S+)");

    public static readonly Regex ExtendsTypeNameRegex =
        new("(?:extends )(?'TypeName'\\S+)");

    /// <summary>
    /// Given a string value of <c>"clearWatch(watchId: number): void;"</c>, the
    /// following capture groups would be present:
    /// <list type="bullet">
    /// <item><c>MethodName</c>: <c>"clearWatch"</c></item>
    /// <item><c>Parameters</c>: <c>"(watchId: number)"</c></item>
    /// <item><c>ReturnType</c>: <c>": void;"</c></item>
    /// </list>
    /// </summary>
    public static readonly Regex TypeScriptMethodRegex =
        new(@"^(?'MethodName'\S+(?=\())(?'Parameters'.*\))(?'ReturnType'\:.*)$", RegexOptions.Multiline);

    /// <summary>
    /// Given a string value of <c>"(position: GeolocationPosition): void;"</c>, the
    /// following capture groups would be present:
    /// <list type="bullet">
    /// <item><c>Parameters</c>: <c>"(position: GeolocationPosition"</c></item>
    /// <item><c>ReturnType</c>: <c>": void;"</c></item>
    /// </list>
    /// </summary>
    public static readonly Regex TypeScriptCallbackRegex =
        new(@"^(?'Parameters'\(.*\))(?'ReturnType'\:.*)$", RegexOptions.Multiline);

    public static readonly Regex TypeScriptPropertyRegex =
        new(@"^(?'Name'.*)\:(?:.{1})(?'Type'.*)\;$", RegexOptions.Multiline);

    public static readonly Regex ArrayValuesRegex =
        new(@"\[(?'Values'[^[\]]*)\]", RegexOptions.Multiline);
}
