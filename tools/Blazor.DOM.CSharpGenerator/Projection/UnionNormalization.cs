using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

internal enum UnionSpecialArm
{
    None,
    Null,
    Undefined,
}

internal sealed record NormalizedUnionArm(
    TypeNode Type,
    int SourceIndex,
    string Fingerprint,
    UnionSpecialArm Special,
    IReadOnlyList<string> Provenances);

internal sealed record NormalizedUnion(IReadOnlyList<NormalizedUnionArm> Arms)
{
    public bool HasNull => Arms.Any(arm => arm.Special == UnionSpecialArm.Null);
    public bool HasUndefined => Arms.Any(arm => arm.Special == UnionSpecialArm.Undefined);
    public IReadOnlyList<NormalizedUnionArm> ValueArms
        => Arms.Where(arm => arm.Special == UnionSpecialArm.None).ToList();
}

internal static class UnionNormalization
{
    public static NormalizedUnion Normalize(UnionTypeNode union, string provenance)
    {
        var flattened = new List<(TypeNode Type, int SourceIndex, string Provenance)>();
        for (var index = 0; index < union.Types.Count; index++)
        {
            Flatten(
                union.Types[index],
                index,
                $"{provenance}/arm[{index}]",
                flattened,
                new HashSet<TypeNode>(ReferenceEqualityComparer.Instance));
        }

        var arms = new List<NormalizedUnionArm>();
        var byFingerprint = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in flattened)
        {
            var fingerprint = Fingerprint(item.Type);
            if (byFingerprint.TryGetValue(fingerprint, out var existingIndex))
            {
                var existing = arms[existingIndex];
                arms[existingIndex] = existing with
                {
                    Provenances = [.. existing.Provenances, item.Provenance],
                };
                continue;
            }

            byFingerprint.Add(fingerprint, arms.Count);
            arms.Add(new NormalizedUnionArm(
                item.Type,
                item.SourceIndex,
                fingerprint,
                GetSpecial(item.Type),
                [item.Provenance]));
        }
        return new NormalizedUnion(arms);
    }

    private static void Flatten(
        TypeNode type,
        int sourceIndex,
        string provenance,
        List<(TypeNode Type, int SourceIndex, string Provenance)> result,
        ISet<TypeNode> visiting)
    {
        if (!visiting.Add(type))
        {
            throw new GenericDeferralException(
                $"Recursive union cycle detected at '{provenance}'.",
                provenance,
                "typed-union-cycle");
        }
        try
        {
            switch (type)
            {
                case ParenthesizedTypeNode parenthesized:
                    Flatten(
                        parenthesized.InnerType,
                        sourceIndex,
                        $"{provenance}/parenthesized",
                        result,
                        visiting);
                    break;
                case UnionTypeNode nested:
                    for (var index = 0; index < nested.Types.Count; index++)
                    {
                        Flatten(
                            nested.Types[index],
                            sourceIndex,
                            $"{provenance}/nested[{index}]",
                            result,
                            visiting);
                    }
                    break;
                default:
                    result.Add((type, sourceIndex, provenance));
                    break;
            }
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static UnionSpecialArm GetSpecial(TypeNode type)
        => type switch
        {
            KeywordTypeNode keyword when keyword.Name == "UndefinedKeyword"
                || keyword.CheckerType == "undefined"
                || keyword.Name == "VoidKeyword"
                || keyword.CheckerType == "void" => UnionSpecialArm.Undefined,
            LiteralTypeNode literal when literal.LiteralKind == "UndefinedKeyword"
                => UnionSpecialArm.Undefined,
            KeywordTypeNode keyword when keyword.Name == "NullKeyword"
                || keyword.CheckerType == "null" => UnionSpecialArm.Null,
            LiteralTypeNode literal when literal.LiteralKind is
                "NullKeyword" or "NullLiteral" => UnionSpecialArm.Null,
            _ => UnionSpecialArm.None,
        };

    private static string Fingerprint(TypeNode type)
        => type switch
        {
            KeywordTypeNode keyword =>
                $"keyword:{keyword.CheckerType ?? keyword.Name}",
            ReferenceTypeNode reference =>
                $"reference:{reference.ResolvedSymbol ?? reference.Name}<" +
                $"{string.Join(",", reference.TypeArguments.Select(Fingerprint))}>",
            LiteralTypeNode literal =>
                $"literal:{literal.LiteralKind}:{literal.Text}",
            ArrayTypeNode array => $"array:{Fingerprint(array.ElementType)}",
            TupleTypeNode tuple =>
                $"tuple:[{string.Join(",", tuple.Elements.Select(Fingerprint))}]",
            NamedTupleMemberTypeNode member =>
                $"named:{member.Name}:{member.Optional}:{member.Rest}:" +
                Fingerprint(member.ElementType),
            OptionalTypeNode optional => $"optional:{Fingerprint(optional.InnerType)}",
            RestTypeNode rest => $"rest:{Fingerprint(rest.InnerType)}",
            ParenthesizedTypeNode parenthesized => Fingerprint(parenthesized.InnerType),
            UnionTypeNode union =>
                $"union:{string.Join("|", union.Types.Select(Fingerprint))}",
            TypeLiteralTypeNode literal =>
                $"shape:{string.Join(";", literal.Members.OrderBy(member => member.Ordinal)
                    .Select(member => $"{member.Name?.Text}:{member.Optional}:" +
                        $"{(member.Type is null ? "?" : Fingerprint(member.Type))}"))}",
            FunctionTypeNode function =>
                $"function:({string.Join(",", function.Parameters.Select(parameter =>
                    parameter.Type is null ? "?" : Fingerprint(parameter.Type)))})=>" +
                Fingerprint(function.ReturnType),
            _ => $"{type.Kind}:{type.CheckerType ?? type.SyntaxKind ?? type.ToString()}",
        };
}
