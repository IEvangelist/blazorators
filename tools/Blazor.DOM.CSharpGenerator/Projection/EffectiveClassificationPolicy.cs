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

        // Legacy Window aliases such as SVGMatrix combine a TypeScript type alias
        // with a constructor-object global while Web IDL classifies the binding as
        // an interface alias. There is no interface declaration to emit: the
        // public type shape remains the TypeScript alias.
        if (symbol.Declarations.Any(declaration =>
                declaration.Kind == "typeAlias")
            && !symbol.Declarations.Any(declaration =>
                declaration.Kind == "interface")
            && symbol.Semantic.Classifications.FirstOrDefault()
                is "interface" or "mixin")
        {
            return new EffectiveClassification(
                "typedef",
                EffectiveClassificationSource.DeclarationShape);
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
        {
            return IsJsonValueInterfaceExtension(symbol)
                ? "dictionary"
                : "interface";
        }
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

    private static bool IsJsonValueInterfaceExtension(SymbolModel symbol)
    {
        var declarations = symbol.Declarations
            .Where(declaration => declaration.Kind == "interface")
            .ToList();
        if (declarations.Count == 0
            || declarations.Count != symbol.Declarations.Count
            || declarations.SelectMany(declaration => declaration.Heritage)
                .Any(heritage => heritage.Token != "extends"))
        {
            return false;
        }

        var heritageTypes = declarations
            .SelectMany(declaration => declaration.Heritage)
            .SelectMany(heritage => heritage.Types)
            .ToList();
        return heritageTypes.Count > 0
            && heritageTypes.All(type => type.Transport?.Kind == "json-value")
            && declarations.SelectMany(declaration => declaration.Members)
                .All(member =>
                    (member.Kind is "property" or "getter")
                    && !member.Static
                    && member.Type?.Transport?.Kind == "json-value");
    }
}
