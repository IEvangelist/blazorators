// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;

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
        "The PathFromWindow is required to source generator JavaScript interop",
        "JSAutoInteropAttribute must provide a 'PathFromWindow', as it is required to source generate JavaScript interop extensions",
        "Blazorators.JSAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnableToParseGeneratorOptionsDiagnostic = new(
        "BR0003",
        "The GeneratorOptions required for source generation are unresolvable",
        "JSSerializableAutoInteropAttribute must provide the fully qualified 'Descriptors' type name.",
        "Blazorators.JSSerializableAutoInteropAttribute",
        DiagnosticSeverity.Error,
        true);
}
