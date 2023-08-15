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

    internal static readonly DiagnosticDescriptor UnableToParseGeneratorOptionsDiagnostic = new(
        id: "BR0003",
        title: "The GeneratorOptions required for source generation are unresolvable",
        messageFormat: "JSAutoGenericInteropAttribute must provide the fully qualified 'Descriptors' type name.",
        category: "Blazorators.JSAutoGenericInteropAttribute",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor MissingBlazorSerializationPackageReferenceDiagnostic = new(
        id: "BR0004",
        title: "Missing package reference of Blazor.Serialization",
        messageFormat: "When using JSAutoGenericInteropAttribute you must reference Blazor.Serialization.",
        category: "Blazorators.JSAutoGenericInteropAttribute",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor SourceGenerationFailedDiagnostic = new(
        id: "BR0005",
        title: "Unknown error, send help?!",
        messageFormat: """
            Try deleting your bin and obj folders, clean and try the build again. Exception: {0}
            """,
        category: "Blazor.SourceGenerators.JavaScriptInteropGenerator.TryExecute",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
