// Resolves the transitive closure of generation dependencies for a set of root
// symbols. In addition to the TypeScript type graph, profile generation needs
// the declarations that own routed globals and namespace members.

using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class TransitiveDependencyResolver
{
    /// <summary>
    /// Returns every symbol reachable from <paramref name="rootSymbols"/> through
    /// TypeScript type identities and declaration-routing ownership.
    /// Symbols not present in <paramref name="symbolIndex"/> are included by
    /// identity but not expanded, so profile coverage reports them as external.
    /// </summary>
    public static HashSet<string> Resolve(
        IReadOnlyList<string> rootSymbols,
        IReadOnlyDictionary<string, SymbolModel> symbolIndex)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var rootSymbol in rootSymbols)
            EnqueueName(rootSymbol);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!symbolIndex.TryGetValue(name, out var symbol)) continue;

            EnqueueParentNamespaces(symbol.Name);

            if (string.Equals(
                    symbol.Semantic.BindingKind,
                    "globalMember",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(symbol.Semantic.WebIdlName))
            {
                EnqueueName(symbol.Semantic.WebIdlName);
            }

            foreach (var decl in symbol.Declarations
                .OrderBy(declaration => declaration.Ordinal))
            {
                var declarationScope = AddTypeParameters(
                    EmptyTypeParameterScope,
                    decl.TypeParameters,
                    symbol.Name);
                EnqueueTypeParameters(
                    decl.TypeParameters,
                    declarationScope);
                foreach (var namespaceMember in decl.NamespaceMembers)
                    EnqueueName(namespaceMember);

                // Heritage (extends / implements)
                foreach (var heritage in decl.Heritage)
                    foreach (var type in heritage.Types)
                        Enqueue(type, declarationScope);

                // Members: property types, method return types, parameter types
                foreach (var member in decl.Members
                    .OrderBy(member => member.Ordinal))
                    EnqueueMember(member, declarationScope);

                // Type alias body
                Enqueue(decl.Type, declarationScope);

                // Parameters at declaration level (global functions)
                foreach (var parameter in decl.Parameters
                    .OrderBy(parameter => parameter.Ordinal))
                    Enqueue(parameter.Type, declarationScope);
                Enqueue(decl.ReturnType, declarationScope);
            }
        }

        return closure;

        void EnqueueName(string dependency)
        {
            if (closure.Add(dependency))
                queue.Enqueue(dependency);
        }

        void EnqueueParentNamespaces(string symbolName)
        {
            var separator = symbolName.LastIndexOf('.');
            while (separator > 0)
            {
                EnqueueName(symbolName[..separator]);
                separator = symbolName.LastIndexOf('.', separator - 1);
            }
        }

        void Enqueue(
            TypeNode? node,
            IReadOnlySet<string> typeParameterScope)
        {
            foreach (var dependency in CollectTypeNames(
                node,
                typeParameterScope))
                EnqueueName(dependency);
        }

        void EnqueueMember(
            MemberModel member,
            IReadOnlySet<string> parentScope)
        {
            var memberScope = AddTypeParameters(
                parentScope,
                member.TypeParameters);
            EnqueueTypeParameters(member.TypeParameters, memberScope);
            Enqueue(member.Type, memberScope);
            Enqueue(member.ReturnType, memberScope);
            foreach (var parameter in member.Parameters
                .OrderBy(parameter => parameter.Ordinal))
                Enqueue(parameter.Type, memberScope);
        }

        void EnqueueTypeParameters(
            IReadOnlyList<TypeParameterModel> parameters,
            IReadOnlySet<string> scope)
        {
            foreach (var parameter in parameters)
            {
                Enqueue(parameter.Constraint, scope);
                Enqueue(parameter.Default, scope);
            }
        }
    }

    private static readonly IReadOnlySet<string> EmptyTypeParameterScope =
        new HashSet<string>(StringComparer.Ordinal);

    private static IReadOnlySet<string> AddTypeParameters(
        IReadOnlySet<string> parent,
        IReadOnlyList<TypeParameterModel> parameters,
        string? ownerIdentity = null)
    {
        if (parameters.Count == 0)
            return parent;
        var scope = new HashSet<string>(parent, StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            scope.Add(parameter.Name);
            if (!string.IsNullOrWhiteSpace(ownerIdentity))
                scope.Add($"{ownerIdentity}.{parameter.Name}");
        }
        return scope;
    }

    private static IEnumerable<string> CollectTypeNames(
        TypeNode? node,
        IReadOnlySet<string> typeParameterScope)
    {
        if (node is null) yield break;

        switch (node)
        {
            case ReferenceTypeNode r:
                var referenceIdentity = string.IsNullOrWhiteSpace(r.ResolvedSymbol)
                    ? r.Name
                    : r.ResolvedSymbol;
                if (!string.IsNullOrEmpty(referenceIdentity)
                    && (!typeParameterScope.Contains(referenceIdentity)
                        || r.Name.Contains('.', StringComparison.Ordinal)
                        || !typeParameterScope.Contains(r.Name)))
                    yield return referenceIdentity;
                foreach (var ta in r.TypeArguments)
                    foreach (var n in CollectTypeNames(ta, typeParameterScope))
                        yield return n;
                break;

            case HeritageReferenceTypeNode h:
                var heritageIdentity = string.IsNullOrWhiteSpace(h.ResolvedSymbol)
                    ? h.Expression
                    : h.ResolvedSymbol;
                if (!string.IsNullOrEmpty(heritageIdentity))
                    yield return heritageIdentity;
                foreach (var ta in h.TypeArguments)
                    foreach (var n in CollectTypeNames(ta, typeParameterScope))
                        yield return n;
                break;

            case UnionTypeNode u:
                foreach (var t in u.Types)
                    foreach (var n in CollectTypeNames(t, typeParameterScope))
                        yield return n;
                break;

            case IntersectionTypeNode i:
                foreach (var t in i.Types)
                    foreach (var n in CollectTypeNames(t, typeParameterScope))
                        yield return n;
                break;

            case ArrayTypeNode a:
                foreach (var n in CollectTypeNames(
                    a.ElementType,
                    typeParameterScope))
                    yield return n;
                break;

            case TupleTypeNode tup:
                foreach (var t in tup.Elements)
                    foreach (var n in CollectTypeNames(t, typeParameterScope))
                        yield return n;
                break;

            case FunctionTypeNode f:
                var functionScope = AddTypeParameters(
                    typeParameterScope,
                    f.TypeParameters);
                foreach (var typeParameter in f.TypeParameters)
                {
                    foreach (var n in CollectTypeNames(
                        typeParameter.Constraint,
                        functionScope))
                        yield return n;
                    foreach (var n in CollectTypeNames(
                        typeParameter.Default,
                        functionScope))
                        yield return n;
                }
                foreach (var p in f.Parameters)
                    foreach (var n in CollectTypeNames(p.Type, functionScope))
                        yield return n;
                foreach (var n in CollectTypeNames(f.ReturnType, functionScope))
                    yield return n;
                break;

            case TypeLiteralTypeNode tl:
                foreach (var m in tl.Members)
                {
                    var memberScope = AddTypeParameters(
                        typeParameterScope,
                        m.TypeParameters);
                    foreach (var typeParameter in m.TypeParameters)
                    {
                        foreach (var n in CollectTypeNames(
                            typeParameter.Constraint,
                            memberScope))
                            yield return n;
                        foreach (var n in CollectTypeNames(
                            typeParameter.Default,
                            memberScope))
                            yield return n;
                    }
                    foreach (var n in CollectTypeNames(m.Type, memberScope))
                        yield return n;
                    foreach (var n in CollectTypeNames(m.ReturnType, memberScope))
                        yield return n;
                    foreach (var p in m.Parameters)
                        foreach (var n in CollectTypeNames(p.Type, memberScope))
                            yield return n;
                }
                break;

            case QueryTypeNode query:
                var queryIdentity = string.IsNullOrWhiteSpace(query.ResolvedSymbol)
                    ? query.ExpressionName
                    : query.ResolvedSymbol;
                if (!string.IsNullOrEmpty(queryIdentity))
                    yield return queryIdentity;
                foreach (var n in CollectTypeNames(
                    query.ExprType,
                    typeParameterScope))
                    yield return n;
                foreach (var argument in query.TypeArguments ?? [])
                    foreach (var n in CollectTypeNames(
                        argument,
                        typeParameterScope))
                        yield return n;
                break;

            case OperatorTypeNode op:
                foreach (var n in CollectTypeNames(
                    op.OperandType,
                    typeParameterScope))
                    yield return n;
                break;

            case IndexedAccessTypeNode indexed:
                foreach (var n in CollectTypeNames(
                    indexed.ObjectType,
                    typeParameterScope))
                    yield return n;
                foreach (var n in CollectTypeNames(
                    indexed.IndexType,
                    typeParameterScope))
                    yield return n;
                break;

            case ParenthesizedTypeNode parenthesized:
                foreach (var n in CollectTypeNames(
                    parenthesized.InnerType,
                    typeParameterScope))
                    yield return n;
                break;

            case TemplateLiteralTypeNode template:
                foreach (var part in template.Parts)
                    foreach (var n in CollectTypeNames(
                        part,
                        typeParameterScope))
                        yield return n;
                break;

            // keyword, literal, and unknown nodes have no directly resolved symbol identity.
            default:
                break;
        }
    }
}
