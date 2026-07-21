using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Accounting;

internal static class SourceAccountingShape
{
    internal const string FactoryConstructorPhase = "factory-constructor";
    internal const string GlobalFunctionPhase = "globals";
    internal const string GlobalFunctionReason =
        "Global-function declarations are deferred to the global-namespace emission phase.";

    internal static IReadOnlyList<SourceMemberShape> GetMembers(SymbolModel symbol)
    {
        var members = new List<SourceMemberShape>();
        foreach (var declaration in symbol.Declarations.OrderBy(item => item.Ordinal))
        {
            members.AddRange(declaration.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => CreateMember(
                    symbol,
                    declaration,
                    member,
                    $"{symbol.Name}/decl[{declaration.Ordinal}]/member[{member.Ordinal}]",
                    nestedTypeLiteral: false,
                    callbackObjectForm: false)));

            if (declaration.Kind == "globalVariable"
                && declaration.Type is TypeLiteralTypeNode typeLiteral)
            {
                members.AddRange(typeLiteral.Members
                    .OrderBy(member => member.Ordinal)
                    .Select(member => CreateMember(
                        symbol,
                        declaration,
                        member,
                        $"{symbol.Name}/decl[{declaration.Ordinal}]/typeLiteral/member[{member.Ordinal}]",
                        nestedTypeLiteral: true,
                        callbackObjectForm: false)));
            }

            if (declaration.Kind == "typeAlias")
            {
                foreach (var typeLiteralArm in EnumerateTypeLiterals(
                    declaration.Type,
                    "type"))
                {
                    members.AddRange(typeLiteralArm.Node.Members
                        .OrderBy(member => member.Ordinal)
                        .Select(member => CreateMember(
                            symbol,
                            declaration,
                            member,
                            $"{symbol.Name}/decl[{declaration.Ordinal}]/" +
                            $"{typeLiteralArm.Path}/member[{member.Ordinal}]",
                            nestedTypeLiteral: false,
                            callbackObjectForm: true)));
                }
            }
        }

        return members;
    }

    internal static IReadOnlyList<SourceOverloadShape> GetOverloads(
        SymbolModel symbol,
        IReadOnlyList<SourceMemberShape>? members = null)
    {
        members ??= GetMembers(symbol);
        var overloads = members
            .Where(member => IsCallableKind(member.Member.Kind))
            .Select(member => new SourceOverloadShape(
                $"{member.QualifiedKey}/overload",
                member.Declaration,
                member.Member.Ordinal,
                member.Member.Name?.Text ?? member.Member.Kind,
                member.Member.Kind,
                member.Member.TypeParameters,
                member.Member.Parameters,
                member.Member.ReturnType,
                member,
                $"{member.Provenance}/overload",
                member.SourceLocation))
            .ToList();

        foreach (var declaration in symbol.Declarations
            .Where(item => item.Kind == "globalFunction")
            .OrderBy(item => item.Ordinal))
        {
            var qualifiedKey =
                $"{symbol.Name}/decl[{declaration.Ordinal}]/globalFunction/overload";
            overloads.Add(new SourceOverloadShape(
                qualifiedKey,
                declaration,
                null,
                declaration.Name,
                declaration.Kind,
                declaration.TypeParameters,
                declaration.Parameters,
                declaration.ReturnType,
                null,
                qualifiedKey,
                FormatLocation(declaration.Location)));
        }

        foreach (var declaration in symbol.Declarations
            .Where(item => item.Kind == "typeAlias")
            .OrderBy(item => item.Ordinal))
        {
            foreach (var function in EnumerateFunctions(declaration.Type, "type"))
            {
                var qualifiedKey =
                    $"{symbol.Name}/decl[{declaration.Ordinal}]/{function.Path}";
                overloads.Add(new SourceOverloadShape(
                    $"{qualifiedKey}/overload",
                    declaration,
                    null,
                    "callSignature",
                    "function",
                    function.Node.TypeParameters,
                    function.Node.Parameters,
                    function.Node.ReturnType,
                    null,
                    $"{qualifiedKey}/function/overload",
                    FormatLocation(declaration.Location)));
            }
        }

        return overloads;
    }

    internal static bool IsFactoryDeclaration(DeclarationModel declaration)
        => declaration.Kind == "globalVariable"
            && declaration.Type is TypeLiteralTypeNode;

    internal static string FactoryReason(DeclarationModel declaration)
        => declaration.ConstructorObject
            ? "Constructor-object members are deferred to the factory/constructor emission phase."
            : "Static global-variable members are deferred to the factory/constructor emission phase.";

    internal static bool IsCallableKind(string kind)
        => kind is "method" or "callSignature" or "constructSignature";

    internal static string FormatLocation(LocationModel location)
    {
        var source = string.IsNullOrWhiteSpace(location.Source)
            ? "unknown"
            : location.Source.Replace('\\', '/');
        return $"{source}:{location.Start.Line}:{location.Start.Column}";
    }

    internal static IReadOnlyList<ParameterOutcome> NormalizeParameterOutcomes(
        SourceOverloadShape source,
        IReadOnlyList<ParameterOutcome> suppliedOutcomes)
    {
        var sourceParameters = source.Parameters
            .OrderBy(parameter => parameter.Ordinal)
            .ToList();
        var duplicateSource = sourceParameters
            .GroupBy(parameter => parameter.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSource is not null)
        {
            throw new InvalidOperationException(
                $"Source overload '{source.QualifiedKey}' contains duplicate parameter " +
                $"ordinal {duplicateSource.Key}.");
        }

        var sourceIndex = sourceParameters.ToDictionary(parameter => parameter.Ordinal);
        var normalized = new Dictionary<int, ParameterOutcome>();
        foreach (var supplied in suppliedOutcomes)
        {
            if (!sourceIndex.TryGetValue(supplied.Ordinal, out var parameter))
            {
                throw new InvalidOperationException(
                    $"Parameter outcome '{source.QualifiedKey}/parameter[{supplied.Ordinal}]' " +
                    "does not identify a source parameter.");
            }
            if (!normalized.TryAdd(
                    parameter.Ordinal,
                    supplied with
                    {
                        Name = parameter.Name,
                        Provenance =
                            $"{source.Provenance}/parameter[{parameter.Ordinal}]/{parameter.Name}",
                        SourceLocation = FormatLocation(parameter.Location),
                    }))
            {
                throw new InvalidOperationException(
                    $"Duplicate parameter outcome for " +
                    $"'{source.QualifiedKey}/parameter[{parameter.Ordinal}]'.");
            }
        }

        var missing = sourceParameters
            .FirstOrDefault(parameter => !normalized.ContainsKey(parameter.Ordinal));
        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"Emitter produced no outcome for " +
                $"'{source.Provenance}/parameter[{missing.Ordinal}]/{missing.Name}'.");
        }

        return sourceParameters
            .Select(parameter => normalized[parameter.Ordinal])
            .ToList();
    }

    private static SourceMemberShape CreateMember(
        SymbolModel symbol,
        DeclarationModel declaration,
        MemberModel member,
        string qualifiedKey,
        bool nestedTypeLiteral,
        bool callbackObjectForm)
        => new(
            qualifiedKey,
            declaration,
            member,
            nestedTypeLiteral,
            callbackObjectForm,
            $"{qualifiedKey}/{member.Kind}/{member.Name?.Text ?? "(unnamed)"}",
            FormatLocation(member.Location));

    private static IEnumerable<(string Path, FunctionTypeNode Node)> EnumerateFunctions(
        TypeNode? node,
        string path)
    {
        switch (node)
        {
            case FunctionTypeNode function:
                yield return ($"{path}/function", function);
                break;
            case ParenthesizedTypeNode parenthesized:
                foreach (var nested in EnumerateFunctions(
                    parenthesized.InnerType,
                    $"{path}/parenthesized"))
                {
                    yield return nested;
                }
                break;
            case UnionTypeNode union:
                for (var index = 0; index < union.Types.Count; index++)
                {
                    foreach (var nested in EnumerateFunctions(
                        union.Types[index],
                        $"{path}/union[{index}]"))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }

    private static IEnumerable<(string Path, TypeLiteralTypeNode Node)> EnumerateTypeLiterals(
        TypeNode? node,
        string path)
    {
        switch (node)
        {
            case TypeLiteralTypeNode typeLiteral:
                yield return ($"{path}/typeLiteral", typeLiteral);
                break;
            case ParenthesizedTypeNode parenthesized:
                foreach (var nested in EnumerateTypeLiterals(
                    parenthesized.InnerType,
                    $"{path}/parenthesized"))
                {
                    yield return nested;
                }
                break;
            case UnionTypeNode union:
                for (var index = 0; index < union.Types.Count; index++)
                {
                    foreach (var nested in EnumerateTypeLiterals(
                        union.Types[index],
                        $"{path}/union[{index}]"))
                    {
                        yield return nested;
                    }
                }
                break;
            case IntersectionTypeNode intersection:
                for (var index = 0; index < intersection.Types.Count; index++)
                {
                    foreach (var nested in EnumerateTypeLiterals(
                        intersection.Types[index],
                        $"{path}/intersection[{index}]"))
                    {
                        yield return nested;
                    }
                }
                break;
        }
    }
}

internal sealed record SourceMemberShape(
    string QualifiedKey,
    DeclarationModel Declaration,
    MemberModel Member,
    bool NestedTypeLiteral,
    bool CallbackObjectForm,
    string Provenance,
    string SourceLocation);

internal sealed record SourceOverloadShape(
    string QualifiedKey,
    DeclarationModel Declaration,
    int? MemberOrdinal,
    string Name,
    string Kind,
    IReadOnlyList<TypeParameterModel> TypeParameters,
    IReadOnlyList<ParameterModel> Parameters,
    TypeNode? ReturnType,
    SourceMemberShape? SourceMember,
    string Provenance,
    string SourceLocation);
