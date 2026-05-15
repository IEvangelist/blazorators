// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Threading;

namespace Blazor.SourceGenerators;

[Generator(LanguageNames.CSharp)]
internal sealed partial class JavaScriptInteropGenerator : IIncrementalGenerator
{
    private const string JSAutoInteropAttributeMetadataName = "JSAutoInteropAttribute";
    private const string JSAutoGenericInteropAttributeMetadataName = "JSAutoGenericInteropAttribute";

    private static readonly HashSet<(string FileName, string SourceCode)> s_sourceCodeToAdd =
    [
        (nameof(RecordCompat).ToGeneratedFileName(), RecordCompat),
        (nameof(BlazorHostingModel).ToGeneratedFileName(), BlazorHostingModel),
        (nameof(JSAutoInteropAttribute).ToGeneratedFileName(), JSAutoInteropAttribute),
        (nameof(JSAutoGenericInteropAttribute).ToGeneratedFileName(), JSAutoGenericInteropAttribute),
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (!System.Diagnostics.Debugger.IsAttached)
        {
            // System.Diagnostics.Debugger.Launch();
        }
#endif

        // Add constant source files.
        context.RegisterPostInitializationOutput(c =>
        {
            // Add source from text.
            foreach (var (fileName, sourceCode) in s_sourceCodeToAdd)
            {
                c.AddSource(fileName,
                    SourceText.From(sourceCode, Encoding.UTF8));
            }
        });

        // `ForAttributeWithMetadataName` (Roslyn 4.4+) is materially faster
        // than `CreateSyntaxProvider` because Roslyn maintains an attribute
        // index per compilation unit, so only nodes whose attribute lists
        // textually mention the metadata name even reach the predicate.
        // Each registration is scoped to a single attribute name; we run
        // one for each interop attribute and union the results.
        var nonGenericTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                JSAutoInteropAttributeMetadataName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => BuildTarget(ctx, isGeneric: false, ct))
            .Where(static t => t is not null);

        var genericTargets = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                JSAutoGenericInteropAttributeMetadataName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, ct) => BuildTarget(ctx, isGeneric: true, ct))
            .Where(static t => t is not null);

        var allTargets = nonGenericTargets.Collect().Combine(genericTargets.Collect());

        context.RegisterSourceOutput(allTargets, Execute);
    }

    private static InteropTarget? BuildTarget(
        GeneratorAttributeSyntaxContext context,
        bool isGeneric,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetNode is not InterfaceDeclarationSyntax interfaceDeclaration)
        {
            return null;
        }

        // `Attributes` holds every matching AttributeData on this node.
        // `JSAutoInteropAttribute` has `AllowMultiple = false`, so taking
        // the first is correct.
        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null)
        {
            return null;
        }

        // NOTE: We deliberately parse the attribute *syntax* rather than the
        // `AttributeData.NamedArguments` collection. Inside the syntax-provider
        // transform, attribute-argument binding has not yet completed, so
        // `NamedArguments` is empty even when the attribute is valid. The
        // syntax path is reliable and matches the original generator's
        // behavior; switching to semantic argument parsing (see T1.11) would
        // require running outside `ForAttributeWithMetadataName`.
        if (attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax attributeSyntax)
        {
            return null;
        }

        // Use the semantic model bound by `ForAttributeWithMetadataName` to
        // evaluate attribute argument expressions. This lets the parser
        // resolve constants, `nameof(...)` results, escaped-quote string
        // literals, enum members, and verbatim/raw strings - shapes the
        // previous syntactic parser silently mangled (T1.11).
        var options = attributeSyntax.GetGeneratorOptions(isGeneric, context.SemanticModel);
        var isPartial = interfaceDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        var containingNamespace = context.TargetSymbol.ContainingNamespace is
            { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString()
                : null;

        var identifierLocation = LocationInfo.CreateFrom(interfaceDeclaration.Identifier.GetLocation());
        var attributeLocation = LocationInfo.CreateFrom(attributeSyntax);

        return new InteropTarget(
            Options: options,
            InterfaceName: interfaceDeclaration.Identifier.ValueText,
            IsPartial: isPartial,
            ContainingNamespace: containingNamespace,
            IsGeneric: isGeneric,
            IdentifierLocation: identifierLocation,
            AttributeLocation: attributeLocation);
    }

    private static void Execute(
        SourceProductionContext context,
        (ImmutableArray<InteropTarget?> NonGeneric, ImmutableArray<InteropTarget?> Generic) inputs)
    {
        var combined = inputs.NonGeneric.Concat(inputs.Generic);

        foreach (var target in combined.Distinct())
        {
            if (target is null)
            {
                continue;
            }

            if (IsDiagnosticError(target.Options, context, target.AttributeLocation))
            {
                continue;
            }

            if (!target.IsPartial)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.MissingPartialModifierDiagnostic,
                        target.IdentifierLocation?.ToLocation() ?? Location.None,
                        target.InterfaceName));
                continue;
            }

            foreach (var parser in target.Options.Parsers)
            {
                var result = parser.ParseTargetType(target.Options.TypeName!);

                switch (result.Status)
                {
                    case ParserResultStatus.SuccessfullyParsed when result.Value is not null:
                        var @interface =
                            target.Options.Implementation!.ToInterfaceName();
                        var implementation =
                            target.Options.Implementation!.ToImplementationName();

                        var topLevelObject = result.Value;

                        context.AddDependentTypesSource(topLevelObject)
                            .AddInterfaceSource(topLevelObject, @interface, target.Options, target.ContainingNamespace)
                            .AddImplementationSource(topLevelObject, implementation, target.Options, target.ContainingNamespace)
                            .AddDependencyInjectionExtensionsSource(topLevelObject, implementation, target.Options);
                        break;

                    case ParserResultStatus.TargetTypeNotFound:
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Descriptors.TargetTypeNotFoundDiagnostic,
                                target.AttributeLocation?.ToLocation() ?? Location.None,
                                target.Options.TypeName));
                        break;

                    case ParserResultStatus.ErrorParsing:
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                Descriptors.TypeParseFailureDiagnostic,
                                target.AttributeLocation?.ToLocation() ?? Location.None,
                                target.Options.TypeName,
                                result.Error ?? "(no error message)"));
                        break;
                }
            }
        }
    }

    static bool IsDiagnosticError(
        GeneratorOptions options,
        SourceProductionContext context,
        LocationInfo? attributeLocation)
    {
        if (options.TypeName is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.TypeNameRequiredDiagnostic,
                    attributeLocation?.ToLocation() ?? Location.None));

            return true;
        }

        if (options.Implementation is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.PathFromWindowRequiredDiagnostic,
                    attributeLocation?.ToLocation() ?? Location.None));

            return true;
        }

        return false;
    }
}
