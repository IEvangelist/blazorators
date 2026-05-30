// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Diagnostics;

static class Descriptors
{
    internal static readonly DiagnosticDescriptor TypeNameRequiredDiagnostic = new(
        "BR0001",
        "TypeName is required to source-generate JavaScript interop",
        "JSAutoInteropAttribute must specify a 'TypeName' value, which names the TypeScript interface the generator should bind to",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor ImplementationRequiredDiagnostic = new(
        "BR0002",
        "Implementation is required to source-generate JavaScript interop",
        "JSAutoInteropAttribute must specify an 'Implementation' value, which is the JavaScript path used to invoke the generated extensions",
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

    internal static readonly DiagnosticDescriptor NotStringLiteralUnionDiagnostic = new(
        "BR0008",
        "Type alias is not a string-literal union",
        "Type alias '{0}' was found but is not a string-literal union (e.g. \"a\" | \"b\" | \"c\"). [JSAutoEnum] only supports string-literal-union aliases. Use [JSAutoType] for object types or remove the [JSAutoEnum] attribute.",
        "Blazorators.JSAutoEnumAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor InvalidEnumProjectionMemberDiagnostic = new(
        "BR0009",
        "String-literal union member produced an invalid or duplicate C# enum identifier",
        "String-literal union '{0}' contains a member that could not be projected into a unique, valid C# enum identifier ({1}). Resolve the collision in the upstream TypeScript declaration or apply [JSAutoEnum] to a different union.",
        "Blazorators.JSAutoEnumAttribute",
        DiagnosticSeverity.Error,
        true);
}
