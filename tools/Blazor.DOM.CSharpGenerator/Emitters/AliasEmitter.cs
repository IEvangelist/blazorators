// Alias emitter: projects TypeScript typeAlias symbols matched to WebIDL "typedef"
// classification into:
//   - C# enums (all-string-literal unions) -> delegates to EnumEmitter
//   - Mixed-union wrapper readonly structs (T | string, etc.)
//   - Simple reference aliases (using X = Y style typedef comments + actual projection)
// FAIL-CLOSED: Hard-errors on unsupported union shapes (no object degradation, no EmitFailedAlias).
// All union arms must project successfully; failed arms throw with provenance.
// #nullable enable is always emitted.

using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Emitters;

public sealed class AliasEmitter(TypeResolver typeResolver, string generatorVersion, string ns)
{
    /// <summary>
    /// Emits C# for a symbol classified as a WebIDL typedef.
    /// May return an enum, a union wrapper, or a pure alias comment block.
    /// Throws <see cref="TypeProjectionException"/> on any projection failure.
    /// </summary>
    public string Emit(SymbolModel symbol)
    {
        var decl = symbol.Declarations.FirstOrDefault(d => d.Kind == "typeAlias")
            ?? throw new InvalidOperationException(
                $"AliasEmitter: '{symbol.Name}' has no typeAlias declaration.");

        var typeNode = decl.Type;
        if (typeNode is null)
            throw new InvalidOperationException($"AliasEmitter: '{symbol.Name}' has null type.");

        var generic = typeResolver.CreateGenericDeclaration(
            symbol,
            symbol.Name);

        // If all-string-literal union -> emit as enum
        if (IsAllStringLiteralUnion(typeNode))
        {
            if (generic.Scope.Parameters.Count > 0)
            {
                throw new GenericDeferralException(
                    $"Generic string-literal alias '{symbol.Name}' cannot be emitted " +
                    "as a non-generic C# enum without losing its type parameters.",
                    $"{symbol.Name}/typeAlias",
                    "generic-alias-enum");
            }
            return EnumEmitter.Emit(symbol, generatorVersion, ns);
        }

        if (IsFiniteStringDomainCandidate(typeNode)
            && typeResolver.TryResolveFiniteStringDomain(
                typeNode,
                $"{symbol.Name}/finiteStringDomain",
                out var keys))
        {
            if (generic.Scope.Parameters.Count > 0)
            {
                throw new GenericDeferralException(
                    $"Generic finite-key alias '{symbol.Name}' cannot be emitted as a " +
                    "non-generic C# enum without losing its type parameters.",
                    $"{symbol.Name}/finiteStringDomain",
                    "finite-key-domain");
            }
            return EnumEmitter.EmitStringValues(
                symbol,
                keys,
                generatorVersion,
                ns);
        }

        // If T | null/undefined union -> simple nullable alias
        if (typeNode is UnionTypeNode un)
        {
            var nonNull = un.Types.Where(t =>
                t is not KeywordTypeNode kw ||
                (kw.Name != "NullKeyword" && kw.Name != "UndefinedKeyword" &&
                 kw.CheckerType != "null" && kw.CheckerType != "undefined"))
                .ToList();

            if (nonNull.Count == 1)
            {
                // T | null -> emit as alias to T?
                // Throws on failure — fail closed (no EmitFailedAlias fallback)
                var proj = typeResolver.Project(
                    nonNull[0],
                    $"{symbol.Name}/inner",
                    generic.Scope);
                return EmitNullableAlias(symbol, decl, proj, generic);
            }

            // Mixed union (not all-null-or-T) -> emit as union wrapper struct
            // All arms must project or throw — no object degradation
            return EmitMixedUnionWrapper(symbol, decl, un, generic);
        }

        // Simple reference or keyword alias
        // Throws on failure — fail closed
        var simpleProj = typeResolver.Project(typeNode, symbol.Name, generic.Scope);
        return EmitSimpleAlias(symbol, decl, simpleProj, generic);
    }

    private static bool IsInterfaceType(string csharpType)
        // Interface types are emitted with the I-prefix convention.
        // A type like IFoo, IFooBar, or IReadOnlyList<...> is an interface.
        // Exclude IReadOnlyList/IEnumerable/IAsyncEnumerable which are BCL interfaces
        // that C# does allow struct conversions for.
    {
        var simpleType = csharpType[(csharpType.LastIndexOf('.') + 1)..];
        return simpleType.StartsWith("I", StringComparison.Ordinal)
            && simpleType.Length > 1
            && char.IsUpper(simpleType[1])
            && !simpleType.StartsWith("IReadOnly", StringComparison.Ordinal)
            && !simpleType.StartsWith("IEnumerable", StringComparison.Ordinal)
            && !simpleType.StartsWith("IAsyncEnumerable", StringComparison.Ordinal);
    }

    private string EmitSimpleAlias(
        SymbolModel symbol,
        DeclarationModel decl,
        TypeProjection proj,
        GenericDeclaration generic)
    {
        var w = new CSharpWriter();
        w.AppendLine("#nullable enable");
        w.AppendLine(CSharpWriter.AutoGeneratedHeader("Blazor.DOM.CSharpGenerator", generatorVersion));
        w.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        w.AppendLine();
        var docText = decl.Documentation?.Text ?? "";
        var deprecated = decl.Documentation?.Deprecated ?? false;
        w.XmlDoc(docText, deprecated);
        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        var innerType = proj.CSharpType;
        var isIface = IsInterfaceType(innerType) || proj.Identity.IsTypeParameter;
        w.AppendLine($"// Typedef alias: {symbol.Name} = {innerType}");
        foreach (var defaultNote in generic.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");
        var declaredName = $"{csName}{generic.TypeParameterList}";
        w.Block(
            $"public readonly struct {declaredName}{generic.ConstraintSuffix}",
            () =>
        {
            w.AppendLine($"public {innerType} Value {{ get; }}");
            w.AppendLine($"public {csName}({innerType} value) => Value = value;");
            if (!isIface)
            {
                w.AppendLine($"public static implicit operator {innerType}({declaredName} a) => a.Value;");
                w.AppendLine($"public static implicit operator {declaredName}({innerType} v) => new(v);");
            }
            else
            {
                // C# does not allow implicit operators from/to interfaces
                w.AppendLine($"public static explicit operator {innerType}({declaredName} a) => a.Value;");
                w.AppendLine($"public static {declaredName} From({innerType} v) => new(v);");
            }
            w.AppendLine("public override string ToString() => $\"{Value}\";");
        });
        return w.ToString();
    }

    private string EmitNullableAlias(
        SymbolModel symbol,
        DeclarationModel decl,
        TypeProjection proj,
        GenericDeclaration generic)
    {
        var w = new CSharpWriter();
        w.AppendLine("#nullable enable");
        w.AppendLine(CSharpWriter.AutoGeneratedHeader("Blazor.DOM.CSharpGenerator", generatorVersion));
        w.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        w.AppendLine();
        var docText = decl.Documentation?.Text ?? "";
        var deprecated = decl.Documentation?.Deprecated ?? false;
        w.XmlDoc(docText, deprecated);
        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        var innerType = proj.CSharpType;
        var isIface = IsInterfaceType(innerType) || proj.Identity.IsTypeParameter;
        w.AppendLine($"// Nullable typedef alias: {symbol.Name} = {innerType}?");
        foreach (var defaultNote in generic.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");
        var declaredName = $"{csName}{generic.TypeParameterList}";
        w.Block(
            $"public readonly struct {declaredName}{generic.ConstraintSuffix}",
            () =>
        {
            w.AppendLine($"public {innerType}? Value {{ get; }}");
            w.AppendLine($"public bool HasValue => Value is not null;");
            w.AppendLine($"public {csName}({innerType}? value) => Value = value;");
            if (!isIface)
            {
                w.AppendLine($"public static implicit operator {innerType}?({declaredName} a) => a.Value;");
                w.AppendLine($"public static implicit operator {declaredName}({innerType}? v) => new(v);");
            }
            else
            {
                // C# does not allow implicit operators from/to interfaces
                w.AppendLine($"public static explicit operator {innerType}?({declaredName} a) => a.Value;");
                w.AppendLine($"public static {declaredName} From({innerType}? v) => new(v);");
            }
            w.AppendLine("public override string ToString() => Value?.ToString() ?? \"(null)\";");
        });
        return w.ToString();
    }

    private string EmitMixedUnionWrapper(
        SymbolModel symbol,
        DeclarationModel decl,
        UnionTypeNode un,
        GenericDeclaration generic)
    {
        if (generic.Scope.Parameters.Count > 0)
        {
            throw new GenericDeferralException(
                $"Generic union alias '{symbol.Name}' requires discriminated named " +
                "factories; implicit conversion operators are ambiguous when type " +
                "arguments coincide.",
                $"{symbol.Name}/typeAlias",
                "typed-union");
        }
        // All arms must project successfully — throws with provenance on any failure.
        // No object fallback arms are permitted.
        var memberTypes = un.Types.Select((t, i) =>
            typeResolver.Project(t, $"{symbol.Name}[{i}]", generic.Scope)
        ).ToList();

        var w = new CSharpWriter();
        w.AppendLine("#nullable enable");
        w.AppendLine(CSharpWriter.AutoGeneratedHeader("Blazor.DOM.CSharpGenerator", generatorVersion));
        w.AppendLine($"namespace {Naming.ToGeneratedNamespace(ns, symbol.Name)};");
        w.AppendLine();
        var docText = decl.Documentation?.Text ?? "";
        var deprecated = decl.Documentation?.Deprecated ?? false;
        w.XmlDoc(docText, deprecated);
        var csName = Naming.ToCSharpSimpleTypeName(symbol.Name);
        var declaredName = $"{csName}{generic.TypeParameterList}";

        w.AppendLine($"// Mixed union wrapper: {symbol.Name} = {string.Join(" | ", memberTypes.Select(t => t.CSharpType))}");
        foreach (var defaultNote in generic.DefaultNotes)
            w.AppendLine($"// TypeScript generic default: {defaultNote}.");
        w.Block(
            $"public readonly struct {declaredName}{generic.ConstraintSuffix}",
            () =>
        {
            w.AppendLine("private readonly object? _value;");
            w.AppendLine($"private {csName}(object? value) => _value = value;");
            w.AppendLine("public object? AsObject => _value;");
            w.AppendLine("public override string ToString() => $\"{_value}\";");
            w.AppendLine();

            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var proj in memberTypes)
            {
                var t = proj.CSharpType;
                // Skip null/void arms; they cannot be meaningfully stored in the union wrapper
                if (t is "null" or "void") continue;
                if (!seenTypes.Add(t)) continue;
                var safeName = t.Replace("[]", "Array").Replace("<", "_").Replace(">", "_")
                    .Replace(",", "_").Replace(" ", "").Replace("?", "Nullable");
                var isIface = IsInterfaceType(t);
                if (isIface)
                {
                    // C# does not allow implicit operators from/to interfaces; use factory methods.
                    w.AppendLine($"public static {declaredName} From{safeName}({t} v) => new(v);");
                }
                else
                {
                    w.AppendLine($"public static implicit operator {declaredName}({t} v) => new(v);");
                }
                w.AppendLine($"public {t}? As{safeName}() => _value is {t} x ? x : default;");
                w.AppendLine($"public bool Is{safeName} => _value is {t};");
            }
        });
        return w.ToString();
    }

    private static bool IsAllStringLiteralUnion(TypeNode typeNode)
    {
        return typeNode switch
        {
            UnionTypeNode un => un.Types.All(t =>
                t is LiteralTypeNode lit && lit.LiteralKind == "StringLiteral"),
            LiteralTypeNode lit => lit.LiteralKind == "StringLiteral",
            _ => false,
        };
    }

    private static bool IsFiniteStringDomainCandidate(TypeNode typeNode)
        => typeNode switch
        {
            OperatorTypeNode { Operator: "KeyOfKeyword" } => true,
            TemplateLiteralTypeNode => true,
            ParenthesizedTypeNode parenthesized
                => IsFiniteStringDomainCandidate(parenthesized.InnerType),
            UnionTypeNode union => union.Types.Any(IsFiniteStringDomainCandidate),
            _ => false,
        };
}
