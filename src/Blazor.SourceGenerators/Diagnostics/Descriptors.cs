// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Diagnostics;

static class Descriptors
{
    internal static readonly DiagnosticDescriptor TypeNameRequiredDiagnostic = new(
        "BR0001",
        "The TypeName is required to source generator JavaScript interop",
        "JSAutoInteropAttribute must provide a 'TypeName', as it is required to source generate JavaScript interop extensions",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor PathFromWindowRequiredDiagnostic = new(
        "BR0002",
        "The Implementation is required to source generator JavaScript interop",
        "JSAutoInteropAttribute must provide a 'Implementation', as it is required to source generate JavaScript interop extensions",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MissingPartialModifierDiagnostic = new(
        "BR0005",
        "Interface decorated with a JS interop attribute must be partial",
        "Interface '{0}' is decorated with a Blazorators JS interop attribute but is not declared 'partial'; no source will be generated. Add the 'partial' modifier so the source generator can extend the type.",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor TargetTypeNotFoundDiagnostic = new(
        "BR0006",
        "TypeName not found in the configured TypeScript declarations",
        "Type '{0}' was not found in the configured TypeScript declarations. Verify the 'TypeName' value matches an interface defined in lib.dom.d.ts (or the supplied TypeDeclarationSources).",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor TypeParseFailureDiagnostic = new(
        "BR0007",
        "Failed to parse TypeScript declaration for the requested TypeName",
        "Failed to parse TypeScript declaration for '{0}': {1}",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);
}
