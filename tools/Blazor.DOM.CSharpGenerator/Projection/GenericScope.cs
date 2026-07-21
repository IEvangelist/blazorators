using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Projection;

public sealed class GenericDeferralException(
    string message,
    string provenance,
    string phase)
    : TypeProjectionException(message, provenance)
{
    public string Phase { get; } = phase;
}

public sealed record GenericParameterBinding(
    string SourceName,
    string CSharpName,
    int Position,
    string CanonicalIdentity,
    TypeParameterModel Model,
    TypeProjection? Substitution = null);

public sealed class GenericScope
{
    private readonly IReadOnlyDictionary<string, GenericParameterBinding> _bySourceName;
    private readonly IReadOnlyDictionary<string, GenericParameterBinding> _byResolvedName;

    private GenericScope(
        GenericScope? parent,
        IReadOnlyList<GenericParameterBinding> parameters,
        string provenance)
    {
        Parent = parent;
        Parameters = parameters;
        Provenance = provenance;
        _bySourceName = parameters.ToDictionary(
            parameter => parameter.SourceName,
            StringComparer.Ordinal);
        var path = provenance.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);
        var lexicalIdentity = string.Join(
            ".",
            path
            .Where(segment =>
                !segment.StartsWith("decl[", StringComparison.Ordinal)
                && !segment.StartsWith("member[", StringComparison.Ordinal)
                && !segment.StartsWith("typeParameter[", StringComparison.Ordinal)));
        _byResolvedName = parameters
            .SelectMany(parameter =>
            {
                var names = new List<string>
                {
                    parameter.SourceName,
                    $"{lexicalIdentity}.{parameter.SourceName}",
                };
                return names.Select(name =>
                    new KeyValuePair<string, GenericParameterBinding>(
                        name,
                        parameter));
            })
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.Ordinal);
    }

    public GenericScope? Parent { get; }
    public IReadOnlyList<GenericParameterBinding> Parameters { get; }
    public string Provenance { get; }

    public static GenericScope Create(
        IReadOnlyList<TypeParameterModel> parameters,
        string provenance,
        GenericScope? parent = null,
        string canonicalPrefix = "!")
    {
        var ordered = parameters.ToList();
        var bindings = new List<GenericParameterBinding>(ordered.Count);
        var normalizedNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var ancestorNames = parent?.GetCSharpNames()
            ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in ordered)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                throw new TypeProjectionException(
                    $"Generic parameter at '{provenance}/typeParameter[{parameter.Ordinal}]' " +
                    "has an empty or whitespace-only name.",
                    $"{provenance}/typeParameter[{parameter.Ordinal}]");
            }

            var csharpName = Naming.ToCSharpTypeParameterName(parameter.Name);
            var identifier = csharpName.TrimStart('@');
            if (identifier.Length == 0
                || !(char.IsLetter(identifier[0]) || identifier[0] == '_')
                || identifier.Skip(1).Any(character =>
                    !(char.IsLetterOrDigit(character) || character == '_')))
            {
                throw new TypeProjectionException(
                    $"Generic parameter '{parameter.Name}' at '{provenance}' " +
                    $"normalizes to invalid C# identifier '{csharpName}'.",
                    $"{provenance}/typeParameter[{parameter.Ordinal}]");
            }
            if (!normalizedNames.TryAdd(csharpName, parameter.Name))
            {
                throw new TypeProjectionException(
                    $"Generic parameters '{normalizedNames[csharpName]}' and " +
                    $"'{parameter.Name}' at '{provenance}' normalize to the duplicate " +
                    $"C# name '{csharpName}'.",
                    $"{provenance}/typeParameter[{parameter.Ordinal}]");
            }
            if (ancestorNames.Contains(csharpName))
            {
                var suffix = 1;
                var renamed = $"{csharpName}_{suffix}";
                while (ancestorNames.Contains(renamed)
                    || normalizedNames.ContainsKey(renamed))
                {
                    renamed = $"{csharpName}_{++suffix}";
                }
                normalizedNames.Remove(csharpName);
                normalizedNames.Add(renamed, parameter.Name);
                csharpName = renamed;
            }

            bindings.Add(new GenericParameterBinding(
                parameter.Name,
                csharpName,
                bindings.Count,
                $"{canonicalPrefix}{bindings.Count}",
                parameter));
        }

        return new GenericScope(parent, bindings, provenance);
    }

    public GenericScope WithSubstitutions(
        IReadOnlyList<TypeProjection> substitutions)
    {
        if (substitutions.Count != Parameters.Count)
            throw new ArgumentException("Substitution count must match generic arity.");
        return new GenericScope(
            Parent,
            Parameters.Select((parameter, index) =>
                parameter with { Substitution = substitutions[index] }).ToList(),
            Provenance);
    }

    public bool TryResolve(
        string sourceName,
        string? resolvedName,
        out GenericParameterBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            if (_byResolvedName.TryGetValue(resolvedName, out binding!))
                return true;
            if (resolvedName.Contains('.', StringComparison.Ordinal))
                return Parent?.TryResolve(sourceName, resolvedName, out binding!) == true;
        }
        if (_bySourceName.TryGetValue(sourceName, out binding!))
            return true;
        return Parent?.TryResolve(sourceName, resolvedName, out binding!) == true;
    }

    public bool ContainsSourceName(string sourceName)
        => _bySourceName.ContainsKey(sourceName)
            || Parent?.ContainsSourceName(sourceName) == true;

    private HashSet<string> GetCSharpNames()
    {
        var names = Parent?.GetCSharpNames()
            ?? new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in Parameters)
            names.Add(parameter.CSharpName);
        return names;
    }
}

public sealed record GenericDeclaration(
    GenericScope Scope,
    string TypeParameterList,
    IReadOnlyList<string> ConstraintClauses,
    string CanonicalConstraints,
    IReadOnlyList<string> DefaultNotes)
{
    public string ConstraintSuffix => ConstraintClauses.Count == 0
        ? ""
        : " " + string.Join(" ", ConstraintClauses);
}
