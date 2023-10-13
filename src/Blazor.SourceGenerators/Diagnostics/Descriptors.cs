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
        "BR0003",
        "The GeneratorOptions required for source generation are unresolvable",
        "JSAutoGenericInteropAttribute must provide the fully qualified 'Descriptors' type name",
        "Blazorators.JSAutoGenericInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MissingBlazorSerializationPackageReferenceDiagnostic = new(
        "BR0004",
        "Missing package reference of Blazor.Serialization",
        "When using JSAutoGenericInteropAttribute you must reference Blazor.Serialization",
        "Blazorators.JSAutoGenericInteropAttribute",
        DiagnosticSeverity.Error,
        true);
}
