// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Threading;

namespace Blazor.SourceGenerators;

/// <summary>
/// C2 - <c>[JSAutoEnum]</c> pipeline branch. Lives in its own partial
/// file so the larger service-projection generator stays focused on the
/// service shape, and so the enum-specific failure modes (BR0006 / BR0008 /
/// BR0009) have an obvious home. The pipeline is wired up in
/// <see cref="JavaScriptInteropGenerator.Initialize"/>.
/// </summary>
internal sealed partial class JavaScriptInteropGenerator
{
    /// <summary>
    /// Transform - converts a <c>[JSAutoEnum]</c> attribute application on a
    /// C# interface anchor into a value-equatable <see cref="EnumTarget"/>.
    /// Pulls <c>TypeName</c> / <c>Namespace</c> / <c>TypeDeclarationSources</c>
    /// from the attribute, infers a missing <c>TypeName</c> from the anchor
    /// name (strips a leading <c>I</c>, drops a trailing <c>Service</c>),
    /// and captures source locations for the downstream diagnostic emit.
    /// <para>
    /// Returns <see langword="null"/> only when the target node isn't an
    /// interface declaration or when the attribute syntax can't be
    /// recovered; everything else (missing TypeName, alias not found,
    /// member-name collision) is surfaced as a diagnostic in
    /// <see cref="ExecuteEnums"/> so the consumer sees an actionable error
    /// rather than silence.
    /// </para>
    /// </summary>
    private static EnumTarget? BuildEnumTarget(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetNode is not InterfaceDeclarationSyntax interfaceDeclaration)
        {
            return null;
        }

        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null)
        {
            return null;
        }

        if (attributeData.ApplicationSyntaxReference?.GetSyntax(cancellationToken)
            is not AttributeSyntax attributeSyntax)
        {
            return null;
        }

        var parsed = attributeSyntax.GetEnumOptions(context.SemanticModel);

        // TypeName inference mirrors the service-projection path so the
        // bare-attribute ergonomic story is identical across [JSAutoInterop]
        // and [JSAutoEnum]: strip leading I, drop a trailing Service.
        var anchorName = interfaceDeclaration.Identifier.ValueText;
        var typeName = string.IsNullOrWhiteSpace(parsed.TypeName)
            ? OptionsInference.InferTypeName(anchorName)
            : parsed.TypeName;

        var containingNamespace = context.TargetSymbol.ContainingNamespace is
            { IsGlobalNamespace: false } ns
                ? ns.ToDisplayString()
                : null;

        var identifierLocation = LocationInfo.CreateFrom(interfaceDeclaration.Identifier.GetLocation());
        var attributeLocation = LocationInfo.CreateFrom(attributeSyntax);

        return new EnumTarget(
            // Stored as empty string when inference produces nothing so the
            // record stays non-null for the pipeline; the executor reports
            // BR0001 (TypeNameRequired) when the value is empty.
            TypeName: typeName ?? string.Empty,
            AnchorInterfaceName: anchorName,
            ContainingNamespace: containingNamespace,
            OverrideNamespace: parsed.Namespace,
            TypeDeclarationSources: parsed.TypeDeclarationSources,
            IdentifierLocation: identifierLocation,
            AttributeLocation: attributeLocation);
    }

    /// <summary>
    /// Source-output handler for the <c>[JSAutoEnum]</c> branch. Scans every
    /// resolved parser (bundled <c>lib.dom.d.ts</c> + any matched
    /// <c>TypeDeclarationSources</c>) for each target, picks the best
    /// classification across all parsers (so a target whose alias is missing
    /// from one source but present in another still emits), then either
    /// delegates to <see cref="EnumProjectionEmitter.TryBuild"/> or reports
    /// one of:
    /// <list type="bullet">
    ///   <item>BR0001 - TypeName missing and inference failed.</item>
    ///   <item>BR0006 - alias not found in any resolved parser.</item>
    ///   <item>BR0008 - alias present but not a string-literal union.</item>
    ///   <item>BR0009 - member-name collision or invalid identifier.</item>
    /// </list>
    /// </summary>
    private static void ExecuteEnums(
        SourceProductionContext context,
        (ImmutableArray<EnumTarget?> Targets,
         ImmutableArray<AdditionalTypeDeclarationSource> AdditionalSources) inputs)
    {
        var additionalSources = inputs.AdditionalSources;
        var cancellationToken = context.CancellationToken;

        // Parser cache shared across enum targets in this Execute pass.
        // Identical key/value strategy to the service pipeline (path-keyed,
        // case-insensitive on Windows) so a workspace targeting the same
        // .d.ts from many [JSAutoEnum] attributes only pays the regex
        // construction cost once.
        var parserCache = new Dictionary<string, TypeDeclarationParser>(
            additionalSources.Length,
            StringComparer.OrdinalIgnoreCase);

        // Dedup key is (effectiveNamespace, enumName) - same TypeName in two
        // different namespaces is legal C# and must not collide on
        // AddSource. Built up as we go so the first occurrence wins,
        // matching the dedup semantics of the service pipeline.
        var seenIdentity = new HashSet<(string? Namespace, string Name)>();

        foreach (var target in inputs.Targets.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(target.TypeName))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.TypeNameRequiredDiagnostic,
                        target.AttributeLocation?.ToLocation() ?? Location.None));
                continue;
            }

            // Build a synthetic GeneratorOptions just so ResolveParsers can
            // honor TypeDeclarationSources without us re-implementing the
            // path-resolution logic. The other fields aren't read.
            var resolverOptions = new GeneratorOptions(SupportsGenerics: false)
            {
                TypeName = target.TypeName,
                Implementation = string.Empty,
                TypeDeclarationSources = target.TypeDeclarationSources,
            };

            var parsers = ResolveParsers(resolverOptions, additionalSources, parserCache);
            if (!parsers.Any())
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptors.TargetTypeNotFoundDiagnostic,
                        target.PreferredDiagnosticLocation(),
                        target.TypeName));
                continue;
            }

            // Scan ALL parsers before emitting. We track the best result
            // observed so far so a multi-source target with one match and
            // one miss still emits successfully (the miss alone shouldn't
            // turn into a BR0006). Ordering of classifications by quality:
            // StringLiteralUnion (success) > NotStringLiteralUnion (BR0008)
            // > AliasNotFound (BR0006).
            var best = TypeDeclarationReader.StringLiteralUnionClassification.AliasNotFound;
            IReadOnlyList<string> bestMembers = Array.Empty<string>();

            foreach (var parser in parsers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var classification = parser.ClassifyStringLiteralUnion(
                    target.TypeName, out var rawMembers);

                if (classification ==
                    TypeDeclarationReader.StringLiteralUnionClassification.StringLiteralUnion)
                {
                    best = classification;
                    bestMembers = rawMembers;
                    break;
                }

                if (classification ==
                        TypeDeclarationReader.StringLiteralUnionClassification.NotStringLiteralUnion
                    && best == TypeDeclarationReader.StringLiteralUnionClassification.AliasNotFound)
                {
                    best = classification;
                }
            }

            switch (best)
            {
                case TypeDeclarationReader.StringLiteralUnionClassification.StringLiteralUnion:
                    EmitEnumOrDiagnose(context, target, bestMembers, seenIdentity);
                    break;

                case TypeDeclarationReader.StringLiteralUnionClassification.NotStringLiteralUnion:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptors.NotStringLiteralUnionDiagnostic,
                            target.PreferredDiagnosticLocation(),
                            target.TypeName));
                    break;

                case TypeDeclarationReader.StringLiteralUnionClassification.AliasNotFound:
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Descriptors.TargetTypeNotFoundDiagnostic,
                            target.PreferredDiagnosticLocation(),
                            target.TypeName));
                    break;
            }
        }
    }

    /// <summary>
    /// Helper - runs the emitter and routes its outcome onto AddSource or
    /// BR0009. Lifted out of <see cref="ExecuteEnums"/> only to keep the
    /// switch inside the per-target loop readable.
    /// </summary>
    private static void EmitEnumOrDiagnose(
        SourceProductionContext context,
        EnumTarget target,
        IReadOnlyList<string> members,
        HashSet<(string? Namespace, string Name)> seenIdentity)
    {
        var effectiveNamespace = target.ResolveEffectiveNamespace();
        var identity = (effectiveNamespace, target.TypeName);

        if (!seenIdentity.Add(identity))
        {
            // Already emitted by an earlier target with the same
            // (namespace, name) pair. Silent skip mirrors the service
            // pipeline's dedup behavior.
            return;
        }

        var result = EnumProjectionEmitter.TryBuild(
            target.TypeName,
            effectiveNamespace,
            members);

        if (!result.Success)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Descriptors.InvalidEnumProjectionMemberDiagnostic,
                    target.PreferredDiagnosticLocation(),
                    target.TypeName,
                    result.ErrorReason ?? "(no reason)"));
            return;
        }

        context.AddSource(result.HintName!, SourceText.From(result.Source!, Encoding.UTF8));
    }
}
