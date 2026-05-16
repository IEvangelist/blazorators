// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Expressions;

internal static class SharedRegex
{
    // These regexes are invoked many times per generator run: once per
    // matched interface/method/property as we parse ~800KB of
    // lib.dom.d.ts plus any consumer-supplied additional `.d.ts`
    // sources. Compiling them up-front pays for itself after a handful
    // of evaluations and avoids re-parsing the pattern on every match.
    // `CultureInvariant` removes culture-sensitive case-folding from the
    // path even though none of these patterns use `IgnoreCase`.
    private const RegexOptions DefaultOptions =
        RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // See: https://regex101.com/r/GV3DiG/1
    public static readonly Regex InterfaceRegex =
        new("^(?'declaration'interface.*?{.*?})$",
            RegexOptions.Singleline | RegexOptions.Multiline | DefaultOptions);

    public static readonly Regex InterfaceTypeNameRegex =
        new("(?:interface )(?'TypeName'\\S+)", DefaultOptions);

    public static readonly Regex ExtendsTypeNameRegex =
        new("(?:extends )(?'TypeName'\\S+)", DefaultOptions);

    public static readonly Regex TypeRegex =
        new("^(?'type'type.*?)$",
            RegexOptions.Singleline | RegexOptions.Multiline | DefaultOptions);

    public static readonly Regex TypeNameRegex =
        new("(?:type )(?'TypeName'\\S+)", DefaultOptions);

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
        new(@"^(?'MethodName'\S+(?=\())(?'Parameters'.*\))(?'ReturnType'\:.*)$",
            RegexOptions.Multiline | DefaultOptions);

    /// <summary>
    /// Given a string value of <c>"(position: GeolocationPosition): void;"</c>, the
    /// following capture groups would be present:
    /// <list type="bullet">
    /// <item><c>Parameters</c>: <c>"(position: GeolocationPosition"</c></item>
    /// <item><c>ReturnType</c>: <c>": void;"</c></item>
    /// </list>
    /// </summary>
    public static readonly Regex TypeScriptCallbackRegex =
        new(@"^(?'Parameters'\(.*\))(?'ReturnType'\:.*)$",
            RegexOptions.Multiline | DefaultOptions);

    public static readonly Regex TypeScriptPropertyRegex =
        new(@"^(?'Name'.*)\:(?:.{1})(?'Type'.*)\;$",
            RegexOptions.Multiline | DefaultOptions);

    public static readonly Regex ArrayValuesRegex =
        new(@"\[(?'Values'[^[\]]*)\]",
            RegexOptions.Multiline | DefaultOptions);
}
