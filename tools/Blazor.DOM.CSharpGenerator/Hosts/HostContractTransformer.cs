using System.Text.Json;
using System.Text.RegularExpressions;
using Blazor.DOM.CSharpGenerator.IR;

namespace Blazor.DOM.CSharpGenerator.Hosts;

public sealed record HostContractResult(
    string Source,
    string ContractType,
    string ProxyType,
    IReadOnlyList<HostApiOperation> Operations);

public sealed partial class HostContractTransformer(DomHostKind host)
{
    private const string DispatchCast =
        "(global::Microsoft.JSInterop.IDomDispatchProxy)this";

    public HostContractResult Transform(SymbolModel symbol, string logicalSource)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalSource);

        var lines = logicalSource
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var output = new List<string>(lines.Length + 16);
        var pending = new List<string>();
        var operations = new List<HostApiOperation>();
        string? contractType = null;
        string? genericParameters = null;
        string? constraints = null;
        IReadOnlyList<string> structuralBases = [];
        var inInterface = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!inInterface && trimmed.StartsWith(
                    "public partial interface ",
                    StringComparison.Ordinal))
            {
                var header = InterfaceHeaderRegex().Match(trimmed);
                if (!header.Success)
                {
                    throw new InvalidOperationException(
                        $"Cannot parse generated interface header '{trimmed}'.");
                }

                contractType = header.Groups["contract"].Value;
                genericParameters = header.Groups["generic"].Value;
                constraints = "";
                var bases = header.Groups["bases"].Value;
                var parsedBases = SplitTopLevel(bases);
                structuralBases = parsedBases
                    .Where(IsStructuralBase)
                    .ToList();
                bases = string.Join(
                    ", ",
                    parsedBases.Where(baseType => !IsStructuralBase(baseType)));
                bases = ProxyBaseRegex().Replace(
                    bases,
                    "global::Microsoft.JSInterop.IDomDispatchProxy");
                output.Add(
                    $"public partial interface {contractType}{genericParameters} : " +
                    $"{bases}{constraints}");
                inInterface = true;
                continue;
            }

            if (!inInterface)
            {
                output.Add(line);
                continue;
            }

            if (trimmed == "{")
            {
                output.Add(line);
                continue;
            }

            if (trimmed == "}")
            {
                output.AddRange(pending);
                pending.Clear();
                EmitStructuralMembers(
                    symbol,
                    structuralBases,
                    output,
                    operations);
                output.Add(line);
                inInterface = false;
                continue;
            }

            if (!IsMemberDeclaration(trimmed))
            {
                pending.Add(line);
                continue;
            }

            var accessorMetadata = pending
                .Select(ParseAccessor)
                .Where(metadata => metadata is not null)
                .Cast<DispatchMetadata>()
                .ToList();
            var operationMetadata = pending
                .Select(ParseOperation)
                .FirstOrDefault(metadata => metadata is not null);
            var indexMetadata = pending
                .Select(ParseIndex)
                .Where(metadata => metadata is not null)
                .Cast<DispatchMetadata>()
                .ToList();

            if (PropertyRegex().IsMatch(trimmed))
            {
                if (accessorMetadata.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Generated property '{symbol.Name}.{trimmed}' has no accessor metadata.");
                }
                EmitProperty(
                    symbol,
                    trimmed,
                    pending,
                    accessorMetadata,
                    output,
                    operations);
            }
            else if (accessorMetadata.Count > 0)
            {
                EmitAccessorMethod(
                    symbol,
                    trimmed,
                    pending,
                    accessorMetadata,
                    output,
                    operations);
            }
            else if (indexMetadata.Count > 0)
            {
                EmitIndexMethod(
                    symbol,
                    trimmed,
                    pending,
                    indexMetadata,
                    output,
                    operations);
            }
            else if (operationMetadata is not null)
            {
                EmitMethod(
                    symbol,
                    trimmed,
                    pending,
                    operationMetadata,
                    output,
                    operations);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Generated method '{symbol.Name}.{trimmed}' has no dispatch metadata.");
            }
            pending.Clear();
        }

        if (contractType is null)
        {
            throw new InvalidOperationException(
                $"Logical source for '{symbol.Name}' did not contain an interface.");
        }

        var proxyType = contractType[1..] + "DomProxy";
        var proxyBase = host == DomHostKind.Server
            ? "global::Microsoft.JSInterop.DomProxyBase"
            : "global::Microsoft.JSInterop.WasmDomProxyBase";
        output.Add("");
        output.Add(
            $"public sealed class {proxyType}{genericParameters}(");
        output.Add("    global::Microsoft.JSInterop.IJSObjectReference reference,");
        output.Add("    global::Microsoft.JSInterop.IDomRuntime runtime,");
        output.Add("    global::Microsoft.JSInterop.IDomProxyFactory factory)");
        output.Add(
            $"    : {proxyBase}(reference, runtime, factory), " +
            $"{contractType}{genericParameters}{constraints}");
        output.Add("{");
        output.Add("}");

        return new HostContractResult(
            string.Join("\n", output).TrimEnd() + "\n",
            contractType + genericParameters,
            proxyType + genericParameters,
            operations);
    }

    private void EmitProperty(
        SymbolModel symbol,
        string declaration,
        IReadOnlyList<string> pending,
        IReadOnlyList<DispatchMetadata> metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var match = PropertyRegex().Match(declaration);
        var type = match.Groups["type"].Value;
        var name = match.Groups["name"].Value;
        var accessors = match.Groups["accessors"].Value;
        var getter = metadata.SingleOrDefault(item => item.Operation == "Get");
        var setter = metadata.SingleOrDefault(item => item.Operation == "Set");

        if (host == DomHostKind.WebAssembly)
        {
            output.AddRange(pending);
            output.Add($"    {type} {name}");
            output.Add("    {");
            if (accessors.Contains("get;", StringComparison.Ordinal))
            {
                output.Add(
                    $"        get => global::Microsoft.JSInterop.WasmDomDispatch." +
                    $"GetProperty<{type}>({DispatchCast}, {getter!.JavaScriptNameLiteral}, " +
                    $"{getter.Descriptor});");
                AddOperation(symbol, "property-get", name, type, getter, operations);
            }
            if (accessors.Contains("set;", StringComparison.Ordinal))
            {
                output.Add(
                    $"        set => global::Microsoft.JSInterop.WasmDomDispatch." +
                    $"SetProperty({DispatchCast}, {setter!.JavaScriptNameLiteral}, value);");
                AddOperation(symbol, "property-set", name, $"void({type})", setter, operations);
            }
            output.Add("    }");
            return;
        }

        var nonAccessorLines = pending.Where(
            line => !line.Contains("DomAccessor(", StringComparison.Ordinal)).ToList();
        if (getter is not null)
        {
            output.AddRange(nonAccessorLines);
            output.Add("    " + getter.Attribute);
            var methodName = $"Get{name.TrimStart('@')}Async";
            var signature =
                $"global::System.Threading.Tasks.ValueTask<{type}> {methodName}(" +
                "global::System.Threading.CancellationToken cancellationToken = default)";
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                $"GetPropertyAsync<{type}>({DispatchCast}, {getter.JavaScriptNameLiteral}, " +
                $"{getter.Descriptor}, cancellationToken);");
            AddOperation(symbol, "property-get", name, signature, getter, operations);
        }
        if (setter is not null)
        {
            if (getter is not null)
                output.Add("");
            output.AddRange(nonAccessorLines.Where(
                line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));
            output.Add("    " + setter.Attribute);
            var methodName = $"Set{name.TrimStart('@')}Async";
            var signature =
                $"global::System.Threading.Tasks.ValueTask {methodName}(" +
                $"{type} value, global::System.Threading.CancellationToken " +
                "cancellationToken = default)";
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                $"SetPropertyAsync({DispatchCast}, {setter.JavaScriptNameLiteral}, value, " +
                "cancellationToken);");
            AddOperation(symbol, "property-set", name, signature, setter, operations);
        }
    }

    private void EmitAccessorMethod(
        SymbolModel symbol,
        string declaration,
        IReadOnlyList<string> pending,
        IReadOnlyList<DispatchMetadata> metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var parsed = ParseMethodDeclaration(declaration);
        var expectedDirection = parsed.ReturnType == "void" ? "Set" : "Get";
        var direction = metadata.SingleOrDefault(item =>
            item.Operation == expectedDirection)
            ?? throw new InvalidOperationException(
                $"Accessor method '{symbol.Name}.{declaration}' has no " +
                $"{expectedDirection.ToLowerInvariant()} metadata.");
        output.AddRange(pending);
        if (direction.Operation == "Get")
        {
            if (host == DomHostKind.Server)
            {
                var name = EnsureAsync(parsed.Name);
                var parameters = AddCancellation(parsed.Parameters, out var cancellationName);
                var signature = BuildMethodSignature(
                    $"global::System.Threading.Tasks.ValueTask<{parsed.ReturnType}>",
                    name,
                    parsed.Generic,
                    parameters,
                    parsed.Constraints);
                output.Add(
                    $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                    $"GetPropertyAsync<{parsed.ReturnType}>({DispatchCast}, " +
                    $"{direction.JavaScriptNameLiteral}, {direction.Descriptor}, " +
                    $"{cancellationName});");
                AddOperation(symbol, "property-get", parsed.Name, signature, direction, operations);
            }
            else
            {
                var signature = declaration.TrimEnd(';');
                output.Add(
                    $"    {signature} => global::Microsoft.JSInterop.WasmDomDispatch." +
                    $"GetProperty<{parsed.ReturnType}>({DispatchCast}, " +
                    $"{direction.JavaScriptNameLiteral}, {direction.Descriptor});");
                AddOperation(symbol, "property-get", parsed.Name, signature, direction, operations);
            }
            return;
        }

        var valueName = parsed.ParameterNames.Single();
        if (host == DomHostKind.Server)
        {
            var name = EnsureAsync(parsed.Name);
            var parameters = AddCancellation(parsed.Parameters, out var cancellationName);
            var signature = BuildMethodSignature(
                "global::System.Threading.Tasks.ValueTask",
                name,
                parsed.Generic,
                parameters,
                parsed.Constraints);
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                $"SetPropertyAsync({DispatchCast}, {direction.JavaScriptNameLiteral}, " +
                $"{valueName}, {cancellationName});");
            AddOperation(symbol, "property-set", parsed.Name, signature, direction, operations);
        }
        else
        {
            var signature = declaration.TrimEnd(';');
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.WasmDomDispatch." +
                $"SetProperty({DispatchCast}, {direction.JavaScriptNameLiteral}, {valueName});");
            AddOperation(symbol, "property-set", parsed.Name, signature, direction, operations);
        }
    }

    private void EmitMethod(
        SymbolModel symbol,
        string declaration,
        IReadOnlyList<string> pending,
        DispatchMetadata metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var parsed = ParseMethodDeclaration(declaration);
        if (TryEmitLockRequest(
            symbol,
            parsed,
            pending,
            metadata,
            output,
            operations))
        {
            return;
        }
        if (TryEmitPerformanceEntryList(
            symbol,
            parsed,
            pending,
            metadata,
            output,
            operations))
        {
            return;
        }
        output.AddRange(pending);
        var asyncDispatch = host == DomHostKind.Server || metadata.Promise;
        if (asyncDispatch)
        {
            var (resultType, isVoid) = UnwrapAsyncReturn(parsed.ReturnType);
            var name = EnsureAsync(parsed.Name);
            var parameters = AddCancellation(parsed.Parameters, out var cancellationName);
            var hostReturn = isVoid
                ? "global::System.Threading.Tasks.ValueTask"
                : $"global::System.Threading.Tasks.ValueTask<{resultType}>";
            var signature = BuildMethodSignature(
                hostReturn,
                name,
                parsed.Generic,
                parameters,
                parsed.Constraints);
            var arguments = BuildArguments(parsed);
            var invocation = isVoid
                ? $"global::Microsoft.JSInterop.DomDispatch.InvokeVoidAsync(" +
                    $"{DispatchCast}, {metadata.JavaScriptNameLiteral}, {arguments}, " +
                    $"{cancellationName})"
                : $"global::Microsoft.JSInterop.DomDispatch.InvokeAsync<{resultType}>(" +
                    $"{DispatchCast}, {metadata.JavaScriptNameLiteral}, {arguments}, " +
                    $"{metadata.Descriptor}, {cancellationName})";
            output.Add($"    {signature} => {invocation};");
            AddOperation(symbol, "method", parsed.Name, signature, metadata, operations);
            return;
        }

        var syncSignature = declaration.TrimEnd(';');
        var syncArguments = BuildArguments(parsed);
        var syncInvocation = parsed.ReturnType == "void"
            ? $"global::Microsoft.JSInterop.WasmDomDispatch.InvokeVoid(" +
                $"{DispatchCast}, {metadata.JavaScriptNameLiteral}, {syncArguments})"
            : $"global::Microsoft.JSInterop.WasmDomDispatch.Invoke<{parsed.ReturnType}>(" +
                $"{DispatchCast}, {metadata.JavaScriptNameLiteral}, {syncArguments}, " +
                $"{metadata.Descriptor})";
        output.Add($"    {syncSignature} => {syncInvocation};");
        AddOperation(symbol, "method", parsed.Name, syncSignature, metadata, operations);
    }

    private static bool TryEmitLockRequest(
        SymbolModel symbol,
        ParsedMethod parsed,
        IReadOnlyList<string> pending,
        DispatchMetadata metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        if (!string.Equals(symbol.Name, "LockManager", StringComparison.Ordinal)
            || !string.Equals(parsed.Name, "RequestAsync", StringComparison.Ordinal)
            || parsed.Parameters.Count != 2
            || !parsed.Parameters[1].Contains(
                "LockGrantedCallback<",
                StringComparison.Ordinal))
        {
            return false;
        }

        output.AddRange(pending.Where(line =>
            !line.Contains("DomOperation(", StringComparison.Ordinal)));
        output.Add(
            "    [global::Microsoft.JSInterop.DomOperation(" +
            "\"web-locks:request\", \"request\", " +
            "global::Microsoft.JSInterop.DomTransportKind.JsonValue, " +
            "\"Promise<T>\", Promise = true, StructuredClone = true)]");
        var callback =
            "global::System.Func<global::Microsoft.JSInterop." +
            "DomBorrowedReference<global::Blazor.DOM.ILock>?, " +
            "global::System.Threading.Tasks.Task<T>> callback";
        var parameters = new[]
        {
            parsed.Parameters[0],
            callback,
            "global::System.Threading.CancellationToken cancellationToken = default",
        };
        var signature = BuildMethodSignature(
            "global::System.Threading.Tasks.ValueTask<T>",
            parsed.Name,
            parsed.Generic,
            parameters,
            parsed.Constraints);
        output.Add(
            $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
            "InvokeReferenceResultCallbackAsync<global::Blazor.DOM.ILock, T>(" +
            $"{DispatchCast}, \"request\", 1, [{parsed.ParameterNames[0]}], " +
            "global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
            "\"Lock | null\", nullable: true), callback, cancellationToken);");
        operations.Add(new HostApiOperation(
            $"{symbol.Name}/web-locks:request",
            symbol.Name,
            "reference-result-callback",
            "request",
            true,
            signature));
        return true;
    }

    private bool TryEmitPerformanceEntryList(
        SymbolModel symbol,
        ParsedMethod parsed,
        IReadOnlyList<string> pending,
        DispatchMetadata metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        if (!string.Equals(
            parsed.ReturnType,
            "PerformanceEntryList",
            StringComparison.Ordinal))
        {
            return false;
        }

        output.AddRange(pending.Where(line =>
            !line.Contains("DomOperation(", StringComparison.Ordinal)));
        output.Add(
            "    [global::Microsoft.JSInterop.DomOperation(" +
            $"\"performance-entry-list:{parsed.Name}\", " +
            $"{metadata.JavaScriptNameLiteral}, " +
            "global::Microsoft.JSInterop.DomTransportKind.JsReference, " +
            "\"PerformanceEntryList\")]");
        const string result =
            "global::Microsoft.JSInterop.IBrowserArray<" +
            "global::Blazor.DOM.IPerformanceEntry>";
        var arguments = BuildArguments(parsed);
        string signature;
        if (host == DomHostKind.Server)
        {
            var parameters = AddCancellation(
                parsed.Parameters,
                out var cancellationName);
            signature = BuildMethodSignature(
                $"global::System.Threading.Tasks.ValueTask<{result}>",
                EnsureAsync(parsed.Name),
                parsed.Generic,
                parameters,
                parsed.Constraints);
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                $"InvokeAsync<{result}>({DispatchCast}, " +
                $"{metadata.JavaScriptNameLiteral}, {arguments}, " +
                "global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
                $"\"PerformanceEntryList\"), {cancellationName});");
        }
        else
        {
            signature = BuildMethodSignature(
                result,
                parsed.Name,
                parsed.Generic,
                parsed.Parameters,
                parsed.Constraints);
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.WasmDomDispatch." +
                $"Invoke<{result}>({DispatchCast}, " +
                $"{metadata.JavaScriptNameLiteral}, {arguments}, " +
                "global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
                "\"PerformanceEntryList\"));");
        }
        operations.Add(new HostApiOperation(
            $"{symbol.Name}/performance-entry-list:{parsed.Name}",
            symbol.Name,
            "reference-list",
            Unquote(metadata.JavaScriptNameLiteral),
            false,
            signature));
        return true;
    }

    private void EmitIndexMethod(
        SymbolModel symbol,
        string declaration,
        IReadOnlyList<string> pending,
        IReadOnlyList<DispatchMetadata> metadata,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var parsed = ParseMethodDeclaration(declaration);
        var direction = metadata.Single();
        output.AddRange(pending);
        string signature;
        if (host == DomHostKind.Server)
        {
            var name = EnsureAsync(parsed.Name);
            var parameters = AddCancellation(parsed.Parameters, out var cancellationName);
            var isGetter = direction.Operation == "Get";
            var returnType = isGetter
                ? $"global::System.Threading.Tasks.ValueTask<{parsed.ReturnType}>"
                : "global::System.Threading.Tasks.ValueTask";
            signature = BuildMethodSignature(
                returnType,
                name,
                parsed.Generic,
                parameters,
                parsed.Constraints);
            var invocation = isGetter
                ? $"global::Microsoft.JSInterop.DomDispatch.GetIndexAsync<" +
                    $"{parsed.ReturnType}>({DispatchCast}, {parsed.ParameterNames[0]}, " +
                    $"{direction.Descriptor}, {cancellationName})"
                : $"global::Microsoft.JSInterop.DomDispatch.SetIndexAsync(" +
                    $"{DispatchCast}, {parsed.ParameterNames[0]}, " +
                    $"{parsed.ParameterNames[1]}, {cancellationName})";
            output.Add($"    {signature} => {invocation};");
        }
        else
        {
            signature = declaration.TrimEnd(';');
            var invocation = direction.Operation == "Get"
                ? $"global::Microsoft.JSInterop.WasmDomDispatch.GetIndex<" +
                    $"{parsed.ReturnType}>({DispatchCast}, {parsed.ParameterNames[0]}, " +
                    $"{direction.Descriptor})"
                : $"global::Microsoft.JSInterop.WasmDomDispatch.SetIndex(" +
                    $"{DispatchCast}, {parsed.ParameterNames[0]}, " +
                    $"{parsed.ParameterNames[1]})";
            output.Add($"    {signature} => {invocation};");
        }
        AddOperation(
            symbol,
            direction.Operation == "Get" ? "index-get" : "index-set",
            parsed.Name,
            signature,
            direction,
            operations);
    }

    private static void AddOperation(
        SymbolModel symbol,
        string kind,
        string memberName,
        string signature,
        DispatchMetadata metadata,
        List<HostApiOperation> operations)
    {
        var identity = metadata.LogicalIdentity is not null
            ? $"{symbol.Name}/{metadata.LogicalIdentity}"
            : $"{symbol.Name}/{Unquote(metadata.JavaScriptNameLiteral)}:" +
                metadata.Operation.ToLowerInvariant() +
                (kind.StartsWith("index-", StringComparison.Ordinal)
                    ? $":{memberName}"
                    : "");
        operations.Add(new HostApiOperation(
            identity,
            symbol.Name,
            kind,
            Unquote(metadata.JavaScriptNameLiteral),
            metadata.Promise,
            signature));
    }

    private void EmitStructuralMembers(
        SymbolModel symbol,
        IReadOnlyList<string> bases,
        List<string> output,
        List<HostApiOperation> operations)
    {
        foreach (var baseType in bases)
        {
            if (baseType.Contains("IReadOnlyBrowserMap<", StringComparison.Ordinal)
                || baseType.Contains("IBrowserMap<", StringComparison.Ordinal))
            {
                var arguments = GenericArguments(baseType);
                EmitMapMembers(
                    symbol,
                    arguments[0],
                    arguments[1],
                    baseType.Contains(".IBrowserMap<", StringComparison.Ordinal),
                    output,
                    operations);
            }
            else if (baseType.Contains("IBrowserSet<", StringComparison.Ordinal))
            {
                EmitSetMembers(
                    symbol,
                    GenericArguments(baseType)[0],
                    output,
                    operations);
            }
            else if (baseType.Contains("IBrowserAsyncIterator<", StringComparison.Ordinal))
            {
                EmitIteratorMember(
                    symbol,
                    GenericArguments(baseType),
                    asynchronous: true,
                    output,
                    operations);
            }
            else if (baseType.Contains("IBrowserIterator<", StringComparison.Ordinal))
            {
                EmitIteratorMember(
                    symbol,
                    GenericArguments(baseType),
                    asynchronous: false,
                    output,
                    operations);
            }
            else if (baseType.EndsWith(
                ".ITypeScriptError",
                StringComparison.Ordinal))
            {
                EmitErrorMembers(symbol, output, operations);
            }
        }
    }

    private void EmitMapMembers(
        SymbolModel symbol,
        string keyType,
        string valueType,
        bool mutable,
        List<string> output,
        List<HostApiOperation> operations)
    {
        EmitStructuralGet(
            symbol,
            "size",
            "Size",
            "int",
            [],
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "has",
            "Has",
            "bool",
            [$"{keyType} key"],
            ["key"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "get",
            "Get",
            valueType,
            [$"{keyType} key"],
            ["key"],
            promise: false,
            output,
            operations);
        if (!mutable)
            return;
        EmitStructuralMethod(
            symbol,
            "set",
            "Set",
            "void",
            [$"{keyType} key", $"{valueType} value"],
            ["key", "value"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "delete",
            "Delete",
            "bool",
            [$"{keyType} key"],
            ["key"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "clear",
            "Clear",
            "void",
            [],
            [],
            promise: false,
            output,
            operations);
    }

    private void EmitSetMembers(
        SymbolModel symbol,
        string valueType,
        List<string> output,
        List<HostApiOperation> operations)
    {
        EmitStructuralGet(
            symbol,
            "size",
            "Size",
            "int",
            [],
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "has",
            "Has",
            "bool",
            [$"{valueType} value"],
            ["value"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "add",
            "Add",
            "void",
            [$"{valueType} value"],
            ["value"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "delete",
            "Delete",
            "bool",
            [$"{valueType} value"],
            ["value"],
            promise: false,
            output,
            operations);
        EmitStructuralMethod(
            symbol,
            "clear",
            "Clear",
            "void",
            [],
            [],
            promise: false,
            output,
            operations);
    }

    private void EmitIteratorMember(
        SymbolModel symbol,
        IReadOnlyList<string> arguments,
        bool asynchronous,
        List<string> output,
        List<HostApiOperation> operations)
    {
        var result =
            $"global::Microsoft.JSInterop.BrowserIteratorResult<" +
            $"{arguments[0]}, {arguments[1]}>";
        EmitStructuralMethod(
            symbol,
            "next",
            "Next",
            result,
            [$"{arguments[2]} value"],
            ["value"],
            promise: asynchronous,
            output,
            operations);
    }

    private void EmitErrorMembers(
        SymbolModel symbol,
        List<string> output,
        List<HostApiOperation> operations)
    {
        foreach (var (javaScriptName, csharpName) in new[]
        {
            ("name", "Name"),
            ("message", "Message"),
            ("stack", "Stack"),
        })
        {
            if (operations.Any(operation =>
                string.Equals(
                    operation.JavaScriptName,
                    javaScriptName,
                    StringComparison.Ordinal)))
            {
                continue;
            }
            EmitStructuralGet(
                symbol,
                javaScriptName,
                csharpName,
                "string?",
                [],
                output,
                operations);
        }
    }

    private void EmitStructuralGet(
        SymbolModel symbol,
        string javaScriptName,
        string csharpName,
        string resultType,
        IReadOnlyList<string> parameters,
        List<string> output,
        List<HostApiOperation> operations)
    {
        output.Add("");
        string signature;
        if (host == DomHostKind.Server)
        {
            signature =
                $"global::System.Threading.Tasks.ValueTask<{resultType}> " +
                $"Get{csharpName}Async(global::System.Threading.CancellationToken " +
                "cancellationToken = default)";
            output.Add(
                $"    {signature} => global::Microsoft.JSInterop.DomDispatch." +
                $"GetPropertyAsync<{resultType}>({DispatchCast}, " +
                $"\"{javaScriptName}\", global::Microsoft.JSInterop.DomDispatch." +
                $"InferTransport<{resultType}>(\"{javaScriptName}\"), cancellationToken);");
        }
        else
        {
            signature = $"{resultType} {csharpName} {{ get; }}";
            output.Add(
                $"    {resultType} {csharpName} => " +
                $"global::Microsoft.JSInterop.WasmDomDispatch.GetProperty<{resultType}>(" +
                $"{DispatchCast}, \"{javaScriptName}\", " +
                $"global::Microsoft.JSInterop.DomDispatch.InferTransport<{resultType}>(" +
                $"\"{javaScriptName}\"));");
        }
        AddStructuralOperation(
            symbol,
            javaScriptName,
            "property-get",
            signature,
            promise: false,
            operations);
    }

    private void EmitStructuralMethod(
        SymbolModel symbol,
        string javaScriptName,
        string csharpName,
        string resultType,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> argumentNames,
        bool promise,
        List<string> output,
        List<HostApiOperation> operations)
    {
        output.Add("");
        var arguments = argumentNames.Count == 0
            ? "null"
            : $"[{string.Join(", ", argumentNames)}]";
        var asyncDispatch = host == DomHostKind.Server || promise;
        string signature;
        if (asyncDispatch)
        {
            var methodName = EnsureAsync(csharpName);
            var hostParameters = AddCancellation(parameters, out var cancellationName);
            var returnType = resultType == "void"
                ? "global::System.Threading.Tasks.ValueTask"
                : $"global::System.Threading.Tasks.ValueTask<{resultType}>";
            signature = BuildMethodSignature(
                returnType,
                methodName,
                "",
                hostParameters,
                "");
            var invocation = resultType == "void"
                ? $"global::Microsoft.JSInterop.DomDispatch.InvokeVoidAsync(" +
                    $"{DispatchCast}, \"{javaScriptName}\", {arguments}, " +
                    $"{cancellationName})"
                : $"global::Microsoft.JSInterop.DomDispatch.InvokeAsync<{resultType}>(" +
                    $"{DispatchCast}, \"{javaScriptName}\", {arguments}, " +
                    $"global::Microsoft.JSInterop.DomDispatch.InferTransport<{resultType}>(" +
                    $"\"{javaScriptName}\"), {cancellationName})";
            output.Add($"    {signature} => {invocation};");
        }
        else
        {
            signature =
                $"{resultType} {csharpName}({string.Join(", ", parameters)})";
            var invocation = resultType == "void"
                ? $"global::Microsoft.JSInterop.WasmDomDispatch.InvokeVoid(" +
                    $"{DispatchCast}, \"{javaScriptName}\", {arguments})"
                : $"global::Microsoft.JSInterop.WasmDomDispatch.Invoke<{resultType}>(" +
                    $"{DispatchCast}, \"{javaScriptName}\", {arguments}, " +
                    $"global::Microsoft.JSInterop.DomDispatch.InferTransport<{resultType}>(" +
                    $"\"{javaScriptName}\"))";
            output.Add($"    {signature} => {invocation};");
        }
        AddStructuralOperation(
            symbol,
            javaScriptName,
            "method",
            signature,
            promise,
            operations);
    }

    private static void AddStructuralOperation(
        SymbolModel symbol,
        string javaScriptName,
        string kind,
        string signature,
        bool promise,
        List<HostApiOperation> operations) =>
        operations.Add(new HostApiOperation(
            $"{symbol.Name}/structural:{javaScriptName}",
            symbol.Name,
            kind,
            javaScriptName,
            promise,
            signature));

    private static IReadOnlyList<string> GenericArguments(string type)
    {
        var open = type.IndexOf('<');
        return SplitTopLevel(type[(open + 1)..^1]);
    }

    private static bool IsStructuralBase(string baseType) =>
        baseType.Contains(
            "global::Microsoft.JSInterop.IReadOnlyBrowserMap<",
            StringComparison.Ordinal)
        || baseType.Contains(
            "global::Microsoft.JSInterop.IBrowserMap<",
            StringComparison.Ordinal)
        || baseType.Contains(
            "global::Microsoft.JSInterop.IBrowserSet<",
            StringComparison.Ordinal)
        || baseType.Contains(
            "global::Microsoft.JSInterop.IBrowserIterator<",
            StringComparison.Ordinal)
        || baseType.Contains(
            "global::Microsoft.JSInterop.IBrowserAsyncIterator<",
            StringComparison.Ordinal)
        || baseType.EndsWith(
            "global::Blazor.DOM.StandardTypes.ITypeScriptError",
            StringComparison.Ordinal)
        || baseType.StartsWith(
            "IQueuingStrategyContract<",
            StringComparison.Ordinal);

    private static bool IsMemberDeclaration(string line) =>
        PropertyRegex().IsMatch(line)
        || line.EndsWith(';')
            && !line.StartsWith("//", StringComparison.Ordinal)
            && !line.StartsWith("[", StringComparison.Ordinal)
            && line.Contains('(');

    private static ParsedMethod ParseMethodDeclaration(string declaration)
    {
        var line = declaration.Trim().TrimEnd(';');
        var open = FindTopLevel(line, '(');
        var close = FindMatchingParenthesis(line, open);
        var prefix = line[..open].Trim();
        var suffix = line[(close + 1)..].Trim();
        var split = prefix.LastIndexOf(' ');
        if (open < 0 || close < 0 || split < 0)
            throw new InvalidOperationException($"Cannot parse generated method '{declaration}'.");
        var returnType = prefix[..split].Trim();
        var nameAndGeneric = prefix[(split + 1)..].Trim();
        var genericStart = nameAndGeneric.IndexOf('<');
        var name = genericStart < 0
            ? nameAndGeneric
            : nameAndGeneric[..genericStart];
        var generic = genericStart < 0
            ? ""
            : nameAndGeneric[genericStart..];
        var parameters = SplitTopLevel(line[(open + 1)..close]);
        return new ParsedMethod(
            returnType,
            name,
            generic,
            parameters,
            parameters.Select(ParameterName).ToList(),
            suffix);
    }

    private static IReadOnlyList<string> AddCancellation(
        IReadOnlyList<string> parameters,
        out string cancellationName)
    {
        cancellationName = "cancellationToken";
        var result = parameters.ToList();
        var cancellation =
            "global::System.Threading.CancellationToken cancellationToken = default";
        var restIndex = result.FindIndex(parameter =>
            parameter.TrimStart().StartsWith("params ", StringComparison.Ordinal));
        if (restIndex < 0)
            result.Add(cancellation);
        else
            result.Insert(restIndex, cancellation);
        return result;
    }

    private static string BuildMethodSignature(
        string returnType,
        string name,
        string generic,
        IReadOnlyList<string> parameters,
        string constraints) =>
        $"{returnType} {name}{generic}({string.Join(", ", parameters)}){constraints}";

    private static string BuildArguments(ParsedMethod method)
    {
        var restIndex = method.Parameters
            .Select((parameter, index) => (parameter, index))
            .FirstOrDefault(item =>
                item.parameter.TrimStart().StartsWith("params ", StringComparison.Ordinal))
            .index;
        var hasRest = method.Parameters.Any(parameter =>
            parameter.TrimStart().StartsWith("params ", StringComparison.Ordinal));
        if (!hasRest)
        {
            return method.ParameterNames.Count == 0
                ? "null"
                : $"[{string.Join(", ", method.ParameterNames)}]";
        }

        var fixedNames = method.ParameterNames.Take(restIndex);
        return "global::Microsoft.JSInterop.DomDispatch.CombineArguments(" +
            $"[{string.Join(", ", fixedNames)}], {method.ParameterNames[restIndex]})";
    }

    private static (string ResultType, bool IsVoid) UnwrapAsyncReturn(string returnType)
    {
        const string valueTask = "ValueTask";
        const string globalValueTask = "global::System.Threading.Tasks.ValueTask";
        if (returnType is "void" or valueTask or globalValueTask)
            return ("void", true);
        if (returnType.StartsWith("ValueTask<", StringComparison.Ordinal))
            return (returnType[10..^1], false);
        if (returnType.StartsWith(
                "global::System.Threading.Tasks.ValueTask<",
                StringComparison.Ordinal))
        {
            return (returnType[41..^1], false);
        }
        return (returnType, false);
    }

    private static string EnsureAsync(string name) =>
        name.EndsWith("Async", StringComparison.Ordinal) ? name : name + "Async";

    private static int FindTopLevel(string value, char target)
    {
        var angle = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '<':
                    angle++;
                    break;
                case '>':
                    angle--;
                    break;
                default:
                    if (value[index] == target && angle == 0)
                        return index;
                    break;
            }
        }
        return -1;
    }

    private static int FindMatchingParenthesis(string value, int open)
    {
        var depth = 0;
        for (var index = open; index < value.Length; index++)
        {
            if (value[index] == '(')
                depth++;
            else if (value[index] == ')' && --depth == 0)
                return index;
        }
        return -1;
    }

    private static IReadOnlyList<string> SplitTopLevel(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        var result = new List<string>();
        var start = 0;
        var angle = 0;
        var brackets = 0;
        var parentheses = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '<': angle++; break;
                case '>': angle--; break;
                case '[': brackets++; break;
                case ']': brackets--; break;
                case '(': parentheses++; break;
                case ')': parentheses--; break;
                case ',' when angle == 0 && brackets == 0 && parentheses == 0:
                    result.Add(value[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string ParameterName(string declaration)
    {
        var withoutDefault = declaration.Split(" = ", 2, StringSplitOptions.None)[0];
        var name = withoutDefault[(withoutDefault.LastIndexOf(' ') + 1)..];
        return name;
    }

    private static DispatchMetadata? ParseAccessor(string line) =>
        ParseMetadata(
            AccessorRegex().Match(line),
            logicalIdentityGroup: null,
            line.Trim());

    private static DispatchMetadata? ParseOperation(string line) =>
        ParseMetadata(OperationRegex().Match(line), "identity", line.Trim());

    private static DispatchMetadata? ParseIndex(string line) =>
        ParseMetadata(
            IndexRegex().Match(line),
            logicalIdentityGroup: null,
            line.Trim());

    private static DispatchMetadata? ParseMetadata(
        Match match,
        string? logicalIdentityGroup,
        string attribute)
    {
        if (!match.Success)
            return null;
        var args = match.Groups["args"].Value;
        var kind = match.Groups["kind"].Value;
        var source = match.Groups["source"].Value;
        var nullable = ReadBool(args, "Nullable");
        var streamable = ReadBool(args, "Streamable");
        var structuredClone = ReadBool(args, "StructuredClone");
        var reason = ReadString(args, "UnsupportedReason");
        var descriptor = kind switch
        {
            "JsonValue" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.JsonValue(" +
                $"{source}, nullable: {Bool(nullable)})",
            "JsReference" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.JsReference(" +
                $"{source}, nullable: {Bool(nullable)}, streamable: {Bool(streamable)}, " +
                $"structuredClone: {Bool(structuredClone)})",
            "JsStream" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.JsStream(" +
                $"{source}, nullable: {Bool(nullable)})",
            "Binary" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.Binary(" +
                $"{source}, nullable: {Bool(nullable)}, streamable: {Bool(streamable)})",
            "Transferable" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.Transferable(" +
                $"{source}, nullable: {Bool(nullable)})",
            "Inferred" =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.Inferred(" +
                $"{source}, nullable: {Bool(nullable)})",
            _ =>
                $"global::Microsoft.JSInterop.DomTransportDescriptor.Unsupported(" +
                $"{source}, {reason ?? "\"Missing reviewed transport metadata.\""}, " +
                $"nullable: {Bool(nullable)})",
        };
        return new DispatchMetadata(
            attribute,
            logicalIdentityGroup is null
                ? null
                : Unquote(match.Groups[logicalIdentityGroup].Value),
            match.Groups["name"].Success
                ? match.Groups["name"].Value
                : "\"[]\"",
            match.Groups["operation"].Success
                ? match.Groups["operation"].Value
                : "Invoke",
            ReadBool(args, "Promise"),
            descriptor);
    }

    private static bool ReadBool(string args, string name)
    {
        var match = Regex.Match(
            args,
            $@"(?:^|,\s*){Regex.Escape(name)}\s*=\s*(?<value>true|false)");
        return match.Success
            && bool.Parse(match.Groups["value"].Value);
    }

    private static string? ReadString(string args, string name)
    {
        var match = Regex.Match(
            args,
            $@"(?:^|,\s*){Regex.Escape(name)}\s*=\s*(?<value>""(?:\\.|[^""])*"")");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string Unquote(string literal) =>
        JsonSerializer.Deserialize<string>(literal)
        ?? throw new InvalidOperationException($"Invalid generated string literal {literal}.");

    private static string Bool(bool value) => value ? "true" : "false";

    private sealed record DispatchMetadata(
        string Attribute,
        string? LogicalIdentity,
        string JavaScriptNameLiteral,
        string Operation,
        bool Promise,
        string Descriptor);

    private sealed record ParsedMethod(
        string ReturnType,
        string Name,
        string Generic,
        IReadOnlyList<string> Parameters,
        IReadOnlyList<string> ParameterNames,
        string Constraints);

    private const string StringLiteral = "\"(?:\\\\.|[^\"])*\"";

    [GeneratedRegex(
        @"^public partial interface (?<contract>\w+)(?<generic><[^>]+>)? : (?<bases>.*?)(?<constraints> where .*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex InterfaceHeaderRegex();

    [GeneratedRegex(
        @"global::Microsoft\.JSInterop\.IDom(?:EventTarget)?Proxy",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProxyBaseRegex();

    [GeneratedRegex(
        @"^(?<type>.+?) (?<name>@?\w+) \{ (?<accessors>get;(?: set;)?|set;) \};?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(
        @"DomAccessor\((?<name>""(?:\\.|[^""])*""), .*?DomAccessorOperation\.(?<operation>Get|Set), .*?DomTransportKind\.(?<kind>\w+), (?<source>""(?:\\.|[^""])*""), (?<args>.*)\)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex AccessorRegex();

    [GeneratedRegex(
        @"DomOperation\((?<identity>""(?:\\.|[^""])*""), (?<name>""(?:\\.|[^""])*""), .*?DomTransportKind\.(?<kind>\w+), (?<source>""(?:\\.|[^""])*""), (?<args>.*)\)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex OperationRegex();

    [GeneratedRegex(
        @"DomIndexAccessor\(.*?DomAccessorOperation\.(?<operation>Get|Set), .*?DomIndexKeyKind\.\w+, ""(?:\\.|[^""])*"", .*?DomTransportKind\.(?<kind>\w+), (?<source>""(?:\\.|[^""])*""), (?<args>.*)\)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex IndexRegex();
}
