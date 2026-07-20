using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

public enum DeclarationRouteKind
{
    Interface,
    Dictionary,
    Typedef,
    Enum,
    Callback,
    GlobalVariable,
    GlobalFunction,
    Namespace,
    FactoryConstructor,
}

public sealed record RoutedDeclaration(
    SymbolModel Symbol,
    DeclarationModel Declaration,
    DeclarationRouteKind Route,
    string? TypeScriptNamespace,
    string Reason);

public sealed record SymbolRouting(
    SymbolModel Symbol,
    DeclarationRouteKind? PrimaryRoute,
    IReadOnlyList<RoutedDeclaration> Declarations,
    string? FailureReason)
{
    public bool HasSupplementalDeclarations => Declarations.Any(
        declaration => declaration.Route is
            DeclarationRouteKind.GlobalVariable or
            DeclarationRouteKind.GlobalFunction or
            DeclarationRouteKind.Namespace or
            DeclarationRouteKind.FactoryConstructor);
}

public sealed class DeclarationRoutingPlan
{
    private readonly IReadOnlyDictionary<string, SymbolRouting> _symbols;

    internal DeclarationRoutingPlan(IReadOnlyDictionary<string, SymbolRouting> symbols)
        => _symbols = symbols;

    public IReadOnlyCollection<SymbolRouting> Symbols => _symbols.Values.ToList();

    public SymbolRouting Get(SymbolModel symbol)
        => _symbols.TryGetValue(symbol.Name, out var routing)
            ? routing
            : throw new InvalidOperationException(
                $"No declaration routing was created for '{symbol.Name}'.");

    public IReadOnlyList<RoutedDeclaration> SupplementalDeclarations
        => _symbols.Values
            .SelectMany(symbol => symbol.Declarations)
            .Where(declaration => declaration.Route is
                DeclarationRouteKind.GlobalVariable or
                DeclarationRouteKind.GlobalFunction or
                DeclarationRouteKind.Namespace or
                DeclarationRouteKind.FactoryConstructor)
            .OrderBy(declaration => declaration.Symbol.Ordinal)
            .ThenBy(declaration => declaration.Declaration.Ordinal)
            .ToList();

    public IReadOnlyList<RoutedDeclaration> GetGlobalAliases(
        string ownerSymbol,
        string memberName)
        => SupplementalDeclarations
            .Where(route =>
                route.TypeScriptNamespace is null
                && route.Symbol.Semantic.BindingKind == "globalMember"
                && string.Equals(
                    route.Symbol.Semantic.WebIdlName,
                    ownerSymbol,
                    StringComparison.Ordinal)
                && string.Equals(
                    route.Symbol.Semantic.WebIdlMemberName
                        ?? route.Declaration.Name,
                    memberName,
                    StringComparison.Ordinal))
            .ToList();
}

/// <summary>
/// Routes every TypeScript declaration by its syntax shape. Semantic
/// classification selects the emitter only for interface and type-alias
/// declarations; it can never turn a global or namespace declaration into an
/// interface declaration.
/// </summary>
public static class DeclarationRouter
{
    public static DeclarationRoutingPlan Create(
        IReadOnlyList<SymbolModel> symbols,
        IReadOnlyDictionary<string, EmitterOverrideEntry>? overrides = null)
    {
        overrides ??= new Dictionary<string, EmitterOverrideEntry>(
            StringComparer.Ordinal);
        var knownSymbols = symbols
            .Select(symbol => symbol.Name)
            .ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, SymbolRouting>(StringComparer.Ordinal);

        foreach (var symbol in symbols.OrderBy(symbol => symbol.Ordinal))
        {
            var effective = EffectiveClassificationPolicy.Classify(
                symbol,
                overrides).Name;
            var declarations = new List<RoutedDeclaration>();
            var primaryRoutes = new HashSet<DeclarationRouteKind>();
            string? failure = symbol.Declarations.Count == 0
                ? $"Symbol '{symbol.Name}' contains no TypeScript declarations to route."
                : null;

            foreach (var declaration in symbol.Declarations
                .OrderBy(declaration => declaration.Ordinal))
            {
                var route = RouteDeclaration(
                    symbol,
                    declaration,
                    effective,
                    out var reason,
                    out var routeFailure);
                if (routeFailure is not null)
                {
                    failure ??= routeFailure;
                    continue;
                }

                declarations.Add(new RoutedDeclaration(
                    symbol,
                    declaration,
                    route,
                    GetNamespace(symbol, declaration, route),
                    reason));
                if (route is
                    DeclarationRouteKind.Interface or
                    DeclarationRouteKind.Dictionary or
                    DeclarationRouteKind.Typedef or
                    DeclarationRouteKind.Enum or
                    DeclarationRouteKind.Callback)
                {
                    primaryRoutes.Add(route);
                }

                if (route == DeclarationRouteKind.Namespace)
                {
                    var missingMember = declaration.NamespaceMembers.FirstOrDefault(
                        member => !knownSymbols.Contains(member));
                    if (missingMember is not null)
                    {
                        failure ??=
                            $"Namespace declaration '{symbol.Name}' references unknown " +
                            $"member symbol '{missingMember}'.";
                    }
                }
            }

            if (primaryRoutes.Count > 1)
            {
                failure ??=
                    $"Symbol '{symbol.Name}' requires incompatible primary declaration " +
                    $"routes: {string.Join(", ", primaryRoutes.Order())}.";
            }
            if (declarations.Count != symbol.Declarations.Count)
            {
                failure ??=
                    $"Symbol '{symbol.Name}' has {symbol.Declarations.Count - declarations.Count} " +
                    "declaration(s) without a route.";
            }

            result.Add(
                symbol.Name,
                new SymbolRouting(
                    symbol,
                    primaryRoutes.Count == 1 ? primaryRoutes.Single() : null,
                    declarations,
                    failure));
        }

        return new DeclarationRoutingPlan(result);
    }

    private static DeclarationRouteKind RouteDeclaration(
        SymbolModel symbol,
        DeclarationModel declaration,
        string effectiveClassification,
        out string reason,
        out string? failure)
    {
        failure = null;
        switch (declaration.Kind)
        {
            case "interface":
                var interfaceRoute = effectiveClassification switch
                {
                    "interface" or "mixin" => DeclarationRouteKind.Interface,
                    "dictionary" => DeclarationRouteKind.Dictionary,
                    "callback" or "callbackInterface" => DeclarationRouteKind.Callback,
                    _ => (DeclarationRouteKind?)null,
                };
                if (interfaceRoute is null)
                {
                    failure =
                        $"Interface declaration '{symbol.Name}/decl[{declaration.Ordinal}]' " +
                        $"cannot use semantic classification '{effectiveClassification}'.";
                    reason = failure;
                    return default;
                }
                reason =
                    $"TypeScript interface shape routed to {interfaceRoute.Value}.";
                return interfaceRoute.Value;

            case "typeAlias":
                var aliasRoute = effectiveClassification switch
                {
                    "enum" => DeclarationRouteKind.Enum,
                    "typedef" => DeclarationRouteKind.Typedef,
                    "callback" or "callbackInterface" => DeclarationRouteKind.Callback,
                    "dictionary" => DeclarationRouteKind.Dictionary,
                    _ => (DeclarationRouteKind?)null,
                };
                if (aliasRoute is null)
                {
                    failure =
                        $"Type-alias declaration '{symbol.Name}/decl[{declaration.Ordinal}]' " +
                        $"cannot use semantic classification '{effectiveClassification}'.";
                    reason = failure;
                    return default;
                }
                reason =
                    $"TypeScript type-alias shape routed to {aliasRoute.Value}.";
                return aliasRoute.Value;

            case "globalVariable":
                if (declaration.Type is null)
                {
                    failure =
                        $"Global variable '{symbol.Name}/decl[{declaration.Ordinal}]' " +
                        "does not declare a type.";
                    reason = failure;
                    return default;
                }
                if (declaration.ConstructorObject
                    || declaration.Type is TypeLiteralTypeNode
                    || declaration.Type is QueryTypeNode)
                {
                    reason = declaration.ConstructorObject
                        ? "Constructor-object global routed to logical factory emission."
                        : "Static type-literal global routed to logical factory emission.";
                    return DeclarationRouteKind.FactoryConstructor;
                }
                reason = "Global variable routed to the Window/global-scope contract.";
                return DeclarationRouteKind.GlobalVariable;

            case "globalFunction":
                reason = "Global function routed to the Window/global-scope contract.";
                return DeclarationRouteKind.GlobalFunction;

            case "namespace":
                reason = "Namespace declaration routed to a logical namespace contract.";
                return DeclarationRouteKind.Namespace;

            default:
                failure =
                    $"Unsupported TypeScript declaration kind '{declaration.Kind}' at " +
                    $"'{symbol.Name}/decl[{declaration.Ordinal}]'.";
                reason = failure;
                return default;
        }
    }

    private static string? GetNamespace(
        SymbolModel symbol,
        DeclarationModel declaration,
        DeclarationRouteKind route)
        => route == DeclarationRouteKind.Namespace
            ? symbol.Name
            : Naming.GetTypeScriptNamespace(symbol.Name);
}
