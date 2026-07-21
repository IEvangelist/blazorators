using System.Text.RegularExpressions;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Hosts;

public sealed record HostFactoryResult(
    string Source,
    string ContractType,
    string ProxyType,
    IReadOnlyList<HostApiOperation> Operations);

public sealed partial class HostFactoryTransformer(DomHostKind host)
{
    private const string DispatchCast =
        "(global::Microsoft.JSInterop.IDomDispatchProxy)this";

    public HostFactoryResult Transform(
        SymbolModel symbol,
        string logicalSource,
        string constructorPath)
    {
        var exactNames = symbol.Declarations
            .Where(declaration => declaration.Kind == "globalVariable")
            .SelectMany(declaration => declaration.Type is TypeLiteralTypeNode literal
                ? literal.Members
                : [])
            .Where(member => member.Name is not null)
            .GroupBy(
                member => Naming.ToCSharpMemberName(member.Name!.Text),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Name!.Text,
                StringComparer.Ordinal);
        var output = new List<string>();
        var operations = new List<HostApiOperation>();
        string? contract = null;
        var inInterface = false;
        foreach (var line in logicalSource
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("public partial interface ", StringComparison.Ordinal))
            {
                contract = trimmed["public partial interface ".Length..];
                output.Add($"public partial interface {contract} : " +
                    "global::Microsoft.JSInterop.IDomDispatchProxy");
                inInterface = true;
                continue;
            }
            if (!inInterface || trimmed is "{" or "}" || trimmed.Length == 0
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                output.Add(line);
                if (inInterface && trimmed == "}")
                    inInterface = false;
                continue;
            }

            var property = PropertyRegex().Match(trimmed);
            if (property.Success)
            {
                EmitProperty(
                    symbol,
                    property,
                    exactNames,
                    output,
                    operations);
                continue;
            }
            if (trimmed.EndsWith(';') && trimmed.Contains('('))
            {
                if (TryEmitPerformanceObserverConstructor(
                    symbol,
                    trimmed,
                    output,
                    operations))
                {
                    continue;
                }
                EmitMethod(
                    symbol,
                    trimmed,
                    constructorPath,
                    exactNames,
                    output,
                    operations);
                continue;
            }
            output.Add(line);
        }
        if (contract is null)
            throw new InvalidOperationException($"Factory '{symbol.Name}' has no contract.");
        var proxy = contract[1..^0] + "DomProxy";
        output.Add("");
        output.Add($"public sealed class {proxy}(");
        output.Add("    global::Microsoft.JSInterop.IJSObjectReference reference,");
        output.Add("    global::Microsoft.JSInterop.IDomRuntime runtime,");
        output.Add("    global::Microsoft.JSInterop.IDomProxyFactory factory)");
        output.Add(
            "    : global::Microsoft.JSInterop." +
            (host == DomHostKind.Server ? "DomProxyBase" : "WasmDomProxyBase") +
            $"(reference, runtime, factory), {contract}");
        output.Add("{");
        output.Add("}");
        return new HostFactoryResult(
            string.Join("\n", output).TrimEnd() + "\n",
            contract,
            proxy,
            operations);
    }

    private static bool TryEmitPerformanceObserverConstructor(
        SymbolModel symbol,
        string declaration,
        List<string> output,
        List<HostApiOperation> operations)
    {
        if (!string.Equals(
                symbol.Name,
                "PerformanceObserver",
                StringComparison.Ordinal)
            || !declaration.Contains(
                "Create(PerformanceObserverCallback callback)",
                StringComparison.Ordinal))
        {
            return false;
        }

        const string callback =
            "global::System.Func<global::Microsoft.JSInterop." +
            "DomBorrowedReference<global::Blazor.DOM." +
            "IPerformanceObserverEntryList>, global::Microsoft.JSInterop." +
            "DomBorrowedReference<global::Blazor.DOM.IPerformanceObserver>, " +
            "global::System.Threading.Tasks.Task> callback";
        var signature =
            "global::System.Threading.Tasks.ValueTask<IPerformanceObserver> " +
            $"CreateAsync({callback}, global::System.Threading." +
            "CancellationToken cancellationToken = default)";
        output.Add(
            $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
            "ConstructReferencePairCallbackAsync<IPerformanceObserver, " +
            "IPerformanceObserverEntryList, IPerformanceObserver>(" +
            "(global::Microsoft.JSInterop.IDomDispatchProxy)this, " +
            "\"PerformanceObserver\", 0, null, " +
            "global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
            "\"PerformanceObserverEntryList\"), " +
            "global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
            "\"PerformanceObserver\"), callback, cancellationToken);");
        operations.Add(new HostApiOperation(
            "PerformanceObserver/factory-method:PerformanceObserver:" +
            "Create(PerformanceObserverCallback callback)",
            symbol.Name,
            "constructor-callback",
            "PerformanceObserver",
            false,
            signature));
        return true;
    }

    private void EmitProperty(
        SymbolModel symbol,
        Match property,
        IReadOnlyDictionary<string, string> exactNames,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var type = property.Groups["type"].Value;
        var name = property.Groups["name"].Value;
        var access = property.Groups["access"].Value;
        var jsName = exactNames.GetValueOrDefault(name)
            ?? (name == "Prototype"
                ? "prototype"
                : throw new InvalidOperationException(
                    $"Factory '{symbol.Name}.{name}' has no exact source name."));
        if (host == DomHostKind.Server)
        {
            output.Add(
                $"    global::System.Threading.Tasks.ValueTask<{type}> " +
                $"Get{name}Async(global::System.Threading.CancellationToken " +
                $"cancellationToken = default) => global::Microsoft.JSInterop." +
                $"DomDispatch.GetPropertyAsync<{type}>({DispatchCast}, \"{jsName}\", " +
                $"global::Microsoft.JSInterop.DomDispatch.InferTransport<{type}>(" +
                $"\"{jsName}\"), cancellationToken);");
            if (access.Contains("set;", StringComparison.Ordinal))
            {
                output.Add(
                    $"    global::System.Threading.Tasks.ValueTask Set{name}Async(" +
                    $"{type} value, global::System.Threading.CancellationToken " +
                    $"cancellationToken = default) => global::Microsoft.JSInterop." +
                    $"DomDispatch.SetPropertyAsync({DispatchCast}, \"{jsName}\", value, " +
                    "cancellationToken);");
            }
        }
        else
        {
            output.Add($"    {type} {name}");
            output.Add("    {");
            output.Add(
                $"        get => global::Microsoft.JSInterop.WasmDomDispatch." +
                $"GetProperty<{type}>({DispatchCast}, \"{jsName}\", " +
                $"global::Microsoft.JSInterop.DomDispatch.InferTransport<{type}>(" +
                $"\"{jsName}\"));");
            if (access.Contains("set;", StringComparison.Ordinal))
            {
                output.Add(
                    $"        set => global::Microsoft.JSInterop.WasmDomDispatch." +
                    $"SetProperty({DispatchCast}, \"{jsName}\", value);");
            }
            output.Add("    }");
        }
        operations.Add(new HostApiOperation(
            $"{symbol.Name}/factory-property:{jsName}",
            symbol.Name,
            "factory-property",
            jsName,
            false,
            $"{type} {name}"));
    }

    private void EmitMethod(
        SymbolModel symbol,
        string declaration,
        string constructorPath,
        IReadOnlyDictionary<string, string> exactNames,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var method = ParseMethod(declaration);
        var construct = method.Name is "Create" or "Invoke";
        var promise = TryUnwrapValueTask(
            method.ReturnType,
            out var resultType);
        var sourceMemberName = method.Name.EndsWith("Async", StringComparison.Ordinal)
            ? method.Name[..^5]
            : method.Name;
        var jsName = construct
            ? constructorPath
            : exactNames.GetValueOrDefault(sourceMemberName)
                ?? throw new InvalidOperationException(
                    $"Factory '{symbol.Name}.{method.Name}' has no exact source name.");
        var arguments = method.Names.Count == 0
            ? "null"
            : $"[{string.Join(", ", method.Names)}]";
        string signature;
        if (host == DomHostKind.Server || promise)
        {
            var parameters = AddCancellation(method.Parameters);
            var name = method.Name.EndsWith("Async", StringComparison.Ordinal)
                ? method.Name
                : method.Name + "Async";
            signature = resultType == "void"
                ? $"global::System.Threading.Tasks.ValueTask {name}{method.Generic}(" +
                    $"{string.Join(", ", parameters)})"
                : $"global::System.Threading.Tasks.ValueTask<{resultType}> " +
                    $"{name}{method.Generic}({string.Join(", ", parameters)})";
            var invocation = resultType == "void"
                ? $"global::Microsoft.JSInterop.DomDispatch.InvokeVoidAsync(" +
                    $"{DispatchCast}, \"{jsName}\", {arguments}, cancellationToken)"
                : construct
                ? $"global::Microsoft.JSInterop.DomDispatch.ConstructAsync<" +
                    $"{resultType}>({DispatchCast}, \"{constructorPath}\", " +
                    $"{arguments}, cancellationToken)"
                : $"global::Microsoft.JSInterop.DomDispatch.InvokeAsync<" +
                    $"{resultType}>({DispatchCast}, \"{jsName}\", {arguments}, " +
                    $"global::Microsoft.JSInterop.DomDispatch.InferTransport<" +
                    $"{resultType}>(\"{jsName}\"), cancellationToken)";
            output.Add($"    {signature} => {invocation};");
        }
        else
        {
            signature = declaration.TrimEnd(';');
            var invocation = method.ReturnType == "void"
                ? $"global::Microsoft.JSInterop.WasmDomDispatch.InvokeVoid(" +
                    $"{DispatchCast}, \"{jsName}\", {arguments})"
                : construct
                ? $"global::Microsoft.JSInterop.WasmDomDispatch.Construct<" +
                    $"{method.ReturnType}>({DispatchCast}, \"{constructorPath}\", " +
                    $"{arguments})"
                : $"global::Microsoft.JSInterop.WasmDomDispatch.Invoke<" +
                    $"{method.ReturnType}>({DispatchCast}, \"{jsName}\", {arguments}, " +
                    $"global::Microsoft.JSInterop.DomDispatch.InferTransport<" +
                    $"{method.ReturnType}>(\"{jsName}\"))";
            output.Add($"    {signature} => {invocation};");
        }
        operations.Add(new HostApiOperation(
            $"{symbol.Name}/factory-method:{jsName}:{method.Name}(" +
            $"{string.Join(",", method.Parameters)})",
            symbol.Name,
            construct ? "constructor" : "factory-method",
            jsName,
            promise,
            signature));
    }

    private static bool TryUnwrapValueTask(
        string returnType,
        out string resultType)
    {
        const string valueTask = "ValueTask";
        const string qualifiedValueTask =
            "global::System.Threading.Tasks.ValueTask";
        if (returnType is valueTask or qualifiedValueTask)
        {
            resultType = "void";
            return true;
        }

        foreach (var prefix in new[] { valueTask + "<", qualifiedValueTask + "<" })
        {
            if (returnType.StartsWith(prefix, StringComparison.Ordinal)
                && returnType.EndsWith('>'))
            {
                resultType = returnType[prefix.Length..^1];
                return true;
            }
        }

        resultType = returnType;
        return false;
    }

    private static ParsedMethod ParseMethod(string declaration)
    {
        var line = declaration.TrimEnd(';');
        var open = line.IndexOf('(');
        var close = line.LastIndexOf(')');
        var prefix = line[..open];
        var split = FindTopLevelLastSpace(prefix);
        var parameters = Split(line[(open + 1)..close]);
        var nameAndGeneric = prefix[(split + 1)..];
        var genericStart = nameAndGeneric.IndexOf('<');
        return new ParsedMethod(
            prefix[..split],
            genericStart < 0
                ? nameAndGeneric
                : nameAndGeneric[..genericStart],
            genericStart < 0
                ? ""
                : nameAndGeneric[genericStart..],
            parameters,
            parameters.Select(ParameterName).ToList());
    }

    private static int FindTopLevelLastSpace(string value)
    {
        var depth = 0;
        var result = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '<') depth++;
            else if (value[index] == '>') depth--;
            else if (value[index] == ' ' && depth == 0) result = index;
        }
        return result;
    }

    private static IReadOnlyList<string> AddCancellation(
        IReadOnlyList<string> parameters)
    {
        var result = parameters.ToList();
        var cancellation =
            "global::System.Threading.CancellationToken cancellationToken = default";
        var rest = result.FindIndex(parameter =>
            parameter.StartsWith("params ", StringComparison.Ordinal));
        if (rest < 0) result.Add(cancellation);
        else result.Insert(rest, cancellation);
        return result;
    }

    private static IReadOnlyList<string> Split(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '<') depth++;
            else if (value[index] == '>') depth--;
            else if (value[index] == ',' && depth == 0)
            {
                result.Add(value[start..index].Trim());
                start = index + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string ParameterName(string parameter)
    {
        var declaration = parameter.Split(" = ", 2, StringSplitOptions.None)[0];
        return declaration[(declaration.LastIndexOf(' ') + 1)..];
    }

    private sealed record ParsedMethod(
        string ReturnType,
        string Name,
        string Generic,
        IReadOnlyList<string> Parameters,
        IReadOnlyList<string> Names);

    [GeneratedRegex(
        @"^(?<type>.+?) (?<name>\w+) \{ (?<access>get;(?: set;)?) \}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PropertyRegex();
}
