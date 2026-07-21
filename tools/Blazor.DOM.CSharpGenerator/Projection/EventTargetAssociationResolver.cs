using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

public sealed record EventTargetAssociation(
    string Target,
    IReadOnlyList<string> EventMaps,
    IReadOnlySet<(int DeclarationOrdinal, int MemberOrdinal)> SourceMembers);

/// <summary>
/// Resolves target/EventMap associations from keyed listener constraints and
/// verifies their indexed callback payload access.
/// </summary>
public sealed class EventTargetAssociationResolver(TypeResolver typeResolver)
{
    public EventTargetAssociation? Resolve(SymbolModel target)
    {
        var maps = new HashSet<string>(StringComparer.Ordinal);
        var sources = new HashSet<(int, int)>();
        var operations = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["addEventListener"] = new(StringComparer.Ordinal),
            ["removeEventListener"] = new(StringComparer.Ordinal),
        };

        foreach (var declaration in target.Declarations
            .Where(declaration => declaration.Kind == "interface")
            .OrderBy(declaration => declaration.Ordinal))
        {
            foreach (var member in declaration.Members
                .Where(member =>
                    member.Kind == "method"
                    && member.Name?.Text is "addEventListener" or "removeEventListener"
                    && member.TypeParameters.Count > 0)
                .OrderBy(member => member.Ordinal))
            {
                var memberMaps = ResolveMemberMaps(
                    target.Name,
                    declaration.Ordinal,
                    member);
                foreach (var map in memberMaps)
                {
                    maps.Add(map);
                    operations[member.Name!.Text].Add(map);
                }
                sources.Add((declaration.Ordinal, member.Ordinal));
            }
        }

        if (maps.Count == 0)
            return null;

        foreach (var map in maps)
        {
            if (!operations["addEventListener"].Contains(map)
                || !operations["removeEventListener"].Contains(map))
            {
                throw new TypeProjectionException(
                    $"Event target '{target.Name}' does not retain matching keyed " +
                    $"add/remove listener contracts for EventMap '{map}'.",
                    $"{target.Name}/event-subscription/{map}");
            }
        }

        return new EventTargetAssociation(
            target.Name,
            maps.Order(StringComparer.Ordinal).ToList(),
            sources);
    }

    private IReadOnlyList<string> ResolveMemberMaps(
        string target,
        int declarationOrdinal,
        MemberModel member)
    {
        var maps = new List<string>();
        foreach (var parameter in member.TypeParameters)
        {
            if (parameter.Constraint is not OperatorTypeNode
                {
                    Operator: "keyof" or "KeyOfKeyword",
                    OperandType: ReferenceTypeNode mapReference,
                })
            {
                throw Invalid(
                    target,
                    declarationOrdinal,
                    member,
                    "generic listener key is not constrained by keyof EventMap");
            }

            var map = mapReference.ResolvedSymbol ?? mapReference.Name;
            if (!typeResolver.TryGetSymbol(map, out var mapSymbol)
                || !mapSymbol.Declarations.Any(declaration =>
                    declaration.EventMap.IsEventMap))
            {
                throw Invalid(
                    target,
                    declarationOrdinal,
                    member,
                    $"key constraint '{map}' does not resolve to an EventMap symbol");
            }
            if (!HasIndexedPayload(member, map, parameter.Name))
            {
                throw Invalid(
                    target,
                    declarationOrdinal,
                    member,
                    $"listener payload is not authoritative indexed access {map}[{parameter.Name}]");
            }
            maps.Add(map);
        }

        if (maps.Count == 0)
        {
            throw Invalid(
                target,
                declarationOrdinal,
                member,
                "listener has no authoritative EventMap constraint");
        }
        return maps;
    }

    private static bool HasIndexedPayload(
        MemberModel member,
        string eventMap,
        string keyParameter) =>
        member.Parameters.Any(parameter =>
            ContainsIndexedAccess(parameter.Type, eventMap, keyParameter));

    private static bool ContainsIndexedAccess(
        TypeNode? node,
        string eventMap,
        string keyParameter)
    {
        if (node is null)
            return false;
        if (node is IndexedAccessTypeNode
            {
                ObjectType: ReferenceTypeNode map,
                IndexType: ReferenceTypeNode key,
            })
        {
            return string.Equals(
                    map.ResolvedSymbol ?? map.Name,
                    eventMap,
                    StringComparison.Ordinal)
                && string.Equals(
                    key.ResolvedSymbol ?? key.Name,
                    keyParameter,
                    StringComparison.Ordinal);
        }
        return node switch
        {
            FunctionTypeNode function => function.Parameters.Any(parameter =>
                ContainsIndexedAccess(parameter.Type, eventMap, keyParameter)),
            ParenthesizedTypeNode parenthesized => ContainsIndexedAccess(
                parenthesized.InnerType,
                eventMap,
                keyParameter),
            UnionTypeNode union => union.Types.Any(type =>
                ContainsIndexedAccess(type, eventMap, keyParameter)),
            _ => false,
        };
    }

    private static TypeProjectionException Invalid(
        string target,
        int declarationOrdinal,
        MemberModel member,
        string reason) =>
        new(
            $"Generic event listener '{target}.{member.Name?.Text}' is invalid: {reason}.",
            $"{target}/decl[{declarationOrdinal}]/member[{member.Ordinal}]/" +
            $"{member.Name?.Text}");
}
