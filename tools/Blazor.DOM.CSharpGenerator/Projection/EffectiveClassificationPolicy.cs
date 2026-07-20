using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

public enum EffectiveClassificationSource
{
    Semantic,
    ReviewedOverride,
    DeclarationShape,
    Unresolved,
}

public sealed record EffectiveClassification(
    string Name,
    EffectiveClassificationSource Source);

/// <summary>
/// Defines the single classification used by emitter routing and type projection.
/// Semantic classifications take precedence over declaration shape so dictionaries
/// represented by TypeScript interfaces are never treated as live C# interfaces.
/// </summary>
public static class EffectiveClassificationPolicy
{
    public static EffectiveClassification Classify(
        SymbolModel symbol,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null)
    {
        if (symbol.Semantic.Status == "ambiguous"
            && overrides is not null
            && overrides.TryGetValue(symbol.Name, out var reviewedOverride))
        {
            return new EffectiveClassification(
                reviewedOverride.Classification,
                EffectiveClassificationSource.ReviewedOverride);
        }

        var semanticClassification = symbol.Semantic.Classifications.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(semanticClassification))
        {
            return new EffectiveClassification(
                semanticClassification,
                EffectiveClassificationSource.Semantic);
        }

        if (symbol.Semantic.Status == "unmatched")
        {
            var declarationClassification = ClassifyUnmatchedDeclaration(symbol);
            if (declarationClassification is not null)
            {
                return new EffectiveClassification(
                    declarationClassification,
                    EffectiveClassificationSource.DeclarationShape);
            }
        }

        return new EffectiveClassification(
            symbol.Semantic.Status,
            EffectiveClassificationSource.Unresolved);
    }

    private static string? ClassifyUnmatchedDeclaration(SymbolModel symbol)
    {
        if (symbol.Declarations.Any(d => d.Kind == "interface"))
            return "interface";
        if (symbol.Declarations.Any(d => d.Kind == "typeAlias"))
            return "typedef";
        if (symbol.Declarations.Any(d => d.Kind == "globalFunction"))
            return "globalFunction";
        if (symbol.Declarations.Any(d => d.Kind == "globalVariable"))
            return "globalVariable";
        if (symbol.Declarations.Any(d => d.Kind == "namespace"))
            return "namespace";

        return null;
    }
}
