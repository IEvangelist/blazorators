// Resolves the transitive closure of type dependencies for a set of root symbols.
// Walks the TypeScript IR type graph (ReferenceTypeNode, HeritageReferenceTypeNode, etc.)
// to collect all symbol names reachable from the root set.

using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Profiles;

public static class TransitiveDependencyResolver
{
    /// <summary>
    /// Returns the set of all symbol names reachable from <paramref name="rootSymbols"/>
    /// by following type references within the TypeScript IR.
    /// Symbols not present in <paramref name="symbolIndex"/> are included by name but not expanded
    /// (they will be accounted as generation-failed or excluded by the pipeline).
    /// </summary>
    public static HashSet<string> Resolve(
        IReadOnlyList<string> rootSymbols,
        IReadOnlyDictionary<string, SymbolModel> symbolIndex)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(rootSymbols);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!visited.Add(name)) continue;
            if (!symbolIndex.TryGetValue(name, out var symbol)) continue;

            foreach (var decl in symbol.Declarations)
            {
                // Heritage (extends / implements)
                foreach (var heritage in decl.Heritage)
                    foreach (var type in heritage.Types)
                        Enqueue(type, visited, queue);

                // Members: property types, method return types, parameter types
                foreach (var member in decl.Members)
                {
                    Enqueue(member.Type, visited, queue);
                    Enqueue(member.ReturnType, visited, queue);
                    foreach (var p in member.Parameters)
                        Enqueue(p.Type, visited, queue);
                }

                // Type alias body
                Enqueue(decl.Type, visited, queue);

                // Parameters at declaration level (global functions)
                foreach (var p in decl.Parameters)
                    Enqueue(p.Type, visited, queue);
                Enqueue(decl.ReturnType, visited, queue);
            }
        }

        return visited;
    }

    private static void Enqueue(TypeNode? node, HashSet<string> visited, Queue<string> queue)
    {
        foreach (var name in CollectTypeNames(node))
            if (!visited.Contains(name))
                queue.Enqueue(name);
    }

    private static IEnumerable<string> CollectTypeNames(TypeNode? node)
    {
        if (node is null) yield break;

        switch (node)
        {
            case ReferenceTypeNode r:
                if (!string.IsNullOrEmpty(r.Name)) yield return r.Name;
                foreach (var ta in r.TypeArguments)
                    foreach (var n in CollectTypeNames(ta)) yield return n;
                break;

            case HeritageReferenceTypeNode h:
                if (!string.IsNullOrEmpty(h.Expression)) yield return h.Expression;
                foreach (var ta in h.TypeArguments)
                    foreach (var n in CollectTypeNames(ta)) yield return n;
                break;

            case UnionTypeNode u:
                foreach (var t in u.Types)
                    foreach (var n in CollectTypeNames(t)) yield return n;
                break;

            case IntersectionTypeNode i:
                foreach (var t in i.Types)
                    foreach (var n in CollectTypeNames(t)) yield return n;
                break;

            case ArrayTypeNode a:
                foreach (var n in CollectTypeNames(a.ElementType)) yield return n;
                break;

            case TupleTypeNode tup:
                foreach (var t in tup.Elements)
                    foreach (var n in CollectTypeNames(t)) yield return n;
                break;

            case FunctionTypeNode f:
                foreach (var p in f.Parameters)
                    foreach (var n in CollectTypeNames(p.Type)) yield return n;
                foreach (var n in CollectTypeNames(f.ReturnType)) yield return n;
                break;

            case TypeLiteralTypeNode tl:
                foreach (var m in tl.Members)
                {
                    foreach (var n in CollectTypeNames(m.Type)) yield return n;
                    foreach (var n in CollectTypeNames(m.ReturnType)) yield return n;
                    foreach (var p in m.Parameters)
                        foreach (var n in CollectTypeNames(p.Type)) yield return n;
                }
                break;

            case OperatorTypeNode op:
                foreach (var n in CollectTypeNames(op.OperandType)) yield return n;
                break;

            // keyword, literal, templateLiteral, query, indexedAccess, unknown:
            // no type names to collect that point to IR symbols.
            default:
                break;
        }
    }
}
