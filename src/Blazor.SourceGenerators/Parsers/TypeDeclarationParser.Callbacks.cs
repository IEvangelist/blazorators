// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Parsers;

internal sealed partial class TypeDeclarationParser
{
    /// <summary>
    /// Returns <c>true</c> when the supplied TypeScript declaration represents
    /// a callback interface - i.e., an interface whose entire body consists of
    /// one or more anonymous call signatures (lines whose first non-whitespace
    /// character is <c>(</c>). Examples from <c>lib.dom.d.ts</c>:
    /// <code>
    /// interface PositionCallback {
    ///     (position: GeolocationPosition): void;
    /// }
    /// interface EventListener {
    ///     (evt: Event): void;
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// This replaces a brittle <c>typeName.EndsWith("Callback")</c> heuristic
    /// that misclassified several real callback interfaces in
    /// <c>lib.dom.d.ts</c> (<c>EventListener</c>, <c>VoidFunction</c>,
    /// <c>MediaSessionActionHandler</c>,
    /// <c>OnBeforeUnloadEventHandlerNonNull</c>,
    /// <c>OnErrorEventHandlerNonNull</c>). Shape-based detection also makes
    /// the generator robust against external <c>.d.ts</c> libraries that
    /// follow a different naming convention.
    /// </remarks>
    internal static bool IsCallbackTypeDeclaration(string typeScriptTypeDeclaration)
    {
        if (string.IsNullOrWhiteSpace(typeScriptTypeDeclaration))
        {
            return false;
        }

        var lines = typeScriptTypeDeclaration.Split(['\n']);
        if (lines.Length < 3)
        {
            // A minimum callback declaration is three lines: the header,
            // the call signature, and the closing brace.
            return false;
        }

        // The first line must be an interface header.
        if (InterfaceTypeNameRegex.GetMatchGroupValue(lines[0], "TypeName") is null)
        {
            return false;
        }

        var sawCallSignature = false;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                break;
            }

            // Skip line / block comments; the regex parsers treat them as
            // trivia, so detection should too.
            if (line.StartsWith("//", StringComparison.Ordinal) ||
                line.StartsWith("/*", StringComparison.Ordinal) ||
                line.StartsWith("*", StringComparison.Ordinal))
            {
                continue;
            }

            if (line[0] != '(')
            {
                return false;
            }

            sawCallSignature = true;
        }

        return sawCallSignature;
    }
}
