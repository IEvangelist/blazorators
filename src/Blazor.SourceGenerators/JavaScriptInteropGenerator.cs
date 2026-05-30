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

        // Project `AdditionalFiles` ending in `.d.ts` into value-equatable
        // records so the incremental cache only re-runs the generator when
        // the *contents* of a declaration source actually change. The Roslyn
        // `AdditionalText` instance itself is not value-equatable.
        // See T5.1 in the audit plan.
        var dtsSources = context.AdditionalTextsProvider
            .Where(static t => t.Path.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase))
            .Select(static (t, ct) => new AdditionalTypeDeclarationSource(
                Path: t.Path,
                FileName: System.IO.Path.GetFileName(t.Path),
                Content: t.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var executeInput = allTargets.Combine(dtsSources);

        context.RegisterSourceOutput(executeInput, Execute);
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

        // G1 - apply reasonable defaults for `TypeName` / `Implementation`
        // when the consumer left them unset, so a bare `[JSAutoInterop]` on
        // `IGeolocationService` infers `TypeName = "Geolocation"` and
        // `Implementation = "window.geolocation"`. Explicit attribute
        // arguments always win; when the interface name can't produce a
        // sensible TypeName (e.g. `IService`), inference is skipped and the
        // existing BR0001/BR0002 diagnostics surface the gap to the user.
        options = OptionsInference.ApplyInferredDefaults(
            options,
            interfaceDeclaration.Identifier.ValueText);

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
        ((ImmutableArray<InteropTarget?> NonGeneric, ImmutableArray<InteropTarget?> Generic) Targets,
         ImmutableArray<AdditionalTypeDeclarationSource> AdditionalSources) inputs)
    {
        var combined = inputs.Targets.NonGeneric.Concat(inputs.Targets.Generic);
        var additionalSources = inputs.AdditionalSources;
        var cancellationToken = context.CancellationToken;

        // Build a path-keyed parser cache up front so that N targets pointing
        // at the same `.d.ts` `AdditionalFile` share a single
        // `TypeDeclarationReader`. Each reader scans the declaration text with
        // multiple regexes (~800KB for `lib.dom.d.ts`), so constructing one
        // per target was quadratic in (targets * shared sources).
        var parserCache = new Dictionary<string, TypeDeclarationParser>(
            additionalSources.Length,
            StringComparer.OrdinalIgnoreCase);

        foreach (var target in combined.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            // When the consumer supplies `TypeDeclarationSources`, parse only
            // the matched additional .d.ts files - the embedded lib.dom.d.ts
            // parser is set aside for this target. This makes the option do
            // what its documentation always claimed (T5.1 fix). When the
            // option is empty/null, fall back to the cached default parser
            // exposed by `GeneratorOptions.Parsers`.
            var parsers = ResolveParsers(target.Options, additionalSources, parserCache);
            if (!parsers.Any())
            {
                // `TypeDeclarationSources` was configured but none of the
                // requested entries matched an `AdditionalFile` in the
                // compilation. Surface BR0006 so the consumer notices, rather
                // than silently producing no output.
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.TargetTypeNotFoundDiagnostic,
                        target.AttributeLocation?.ToLocation() ?? Location.None,
                        target.Options.TypeName));
                continue;
            }

            foreach (var parser in parsers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = parser.ParseTargetType(target.Options.TypeName!);

                switch (result.Status)
                {
                    case ParserResultStatus.SuccessfullyParsed when result.Value is not null:
                        var @interface =
                            target.Options.Implementation!.ToInterfaceName();
                        var implementation =
                            target.Options.Implementation!.ToImplementationName();

                        var topLevelObject = result.Value;

                        context.AddDependentTypesSource(topLevelObject, target.ContainingNamespace)
                            .AddInterfaceSource(topLevelObject, @interface, target.Options, target.ContainingNamespace)
                            .AddImplementationSource(topLevelObject, implementation, target.Options, target.ContainingNamespace)
                            .AddDependencyInjectionExtensionsSource(topLevelObject, implementation, target.Options, target.ContainingNamespace);
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
                    Descriptors.ImplementationRequiredDiagnostic,
                    attributeLocation?.ToLocation() ?? Location.None));

            return true;
        }

        return false;
    }

    /// <summary>
    /// Builds the parser set the generator should use for a target. When
    /// <see cref="GeneratorOptions.TypeDeclarationSources"/> is non-empty
    /// the resolver walks the in-pipeline <c>AdditionalFiles</c> snapshot
    /// (<paramref name="additionalSources"/>) and returns a parser per
    /// matched source. When the option is empty/null, falls through to
    /// the cached default parser (embedded <c>lib.dom.d.ts</c>).
    /// </summary>
    /// <remarks>
    /// <paramref name="parserCache"/> is keyed by the matched
    /// <see cref="AdditionalTypeDeclarationSource.Path"/>, scoped to one
    /// <c>Execute</c> invocation. Sharing a single
    /// <see cref="TypeDeclarationParser"/> across all targets that reference
    /// the same <c>.d.ts</c> avoids paying the regex/map-construction cost
    /// once per target.
    /// </remarks>
    private static IEnumerable<TypeDeclarationParser> ResolveParsers(
        GeneratorOptions options,
        ImmutableArray<AdditionalTypeDeclarationSource> additionalSources,
        Dictionary<string, TypeDeclarationParser> parserCache)
    {
        var requested = options.TypeDeclarationSources;
        if (requested is null || requested.Length == 0)
        {
            return options.Parsers;
        }

        var matched = new List<TypeDeclarationParser>();
        foreach (var requestedSource in requested)
        {
            if (string.IsNullOrWhiteSpace(requestedSource))
            {
                continue;
            }

            // Match by basename, full path, or trailing-segment - MSBuild
            // rewrites `AdditionalFiles` paths to absolute, so a bare
            // "my.d.ts" in the attribute still needs to match a transformed
            // "C:\proj\decls\my.d.ts" entry.
            var requestedBasename = System.IO.Path.GetFileName(requestedSource);
            foreach (var src in additionalSources)
            {
                if (string.Equals(src.Path, requestedSource, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(src.FileName, requestedBasename, StringComparison.OrdinalIgnoreCase) ||
                    src.Path.EndsWith(requestedSource, StringComparison.OrdinalIgnoreCase))
                {
                    if (!parserCache.TryGetValue(src.Path, out var parser))
                    {
                        parser = new TypeDeclarationParser(new TypeDeclarationReader(src.Content));
                        parserCache[src.Path] = parser;
                    }

                    matched.Add(parser);
                    break;
                }
            }
        }

        // No matches - the consumer asked for a custom source that isn't
        // wired into AdditionalFiles. Returning an empty set means we drop
        // to the no-op path; the downstream parser will report TargetTypeNotFound
        // for the requested TypeName, which is the right user-facing signal.
        return matched.Count > 0 ? matched : [];
    }
}
