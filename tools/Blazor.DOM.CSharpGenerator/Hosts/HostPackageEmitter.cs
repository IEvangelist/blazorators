using Blazor.DOM.CSharpGenerator.Emitters;
using Blazor.DOM.CSharpGenerator.IR;
using Blazor.DOM.CSharpGenerator.Output;
using Blazor.DOM.CSharpGenerator.Projection;

namespace Blazor.DOM.CSharpGenerator.Hosts;

public sealed record HostPackageGenerationResult(
    HostApiManifest Server,
    HostApiManifest WebAssembly,
    HostParityReport Parity);

public static class HostPackageEmitter
{
    public static HostPackageGenerationResult Emit(
        IrBundle ir,
        OutputWriter writer,
        IReadOnlyDictionary<string, EmitterOverrideEntry> overrides,
        TypeResolver? sharedResolver = null,
        DeclarationRoutingPlan? sharedRouting = null,
        InterfaceEmitter? sharedInterfaceEmitter = null,
        HostPackageOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ir);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(overrides);
        options ??= HostPackageOptions.Exhaustive;
        ValidateEntryPoints(ir, options.Capability);

        var routing = sharedRouting
            ?? DeclarationRouter.Create(ir.TypescriptSymbols, overrides);
        var resolver = sharedResolver ?? new TypeResolver(
            ir.TypescriptSymbols,
            overrides,
            GenerationPipeline.GeneratedNamespace);
        var logicalEmitter = sharedInterfaceEmitter ?? new InterfaceEmitter(
            resolver,
            GenerationPipeline.GeneratorVersion,
            GenerationPipeline.GeneratedNamespace,
            routing);
        var serverTransformer = new HostContractTransformer(DomHostKind.Server);
        var wasmTransformer = new HostContractTransformer(DomHostKind.WebAssembly);
        var serverOperations = new List<HostApiOperation>();
        var wasmOperations = new List<HostApiOperation>();
        var serverFiles = new List<string>();
        var wasmFiles = new List<string>();
        var hostSymbols = new List<string>();
        var proxyRegistrations = new List<ProxyRegistration>();

        foreach (var route in routing.Symbols
            .OrderBy(route => route.Symbol.Ordinal))
        {
            if (route.PrimaryRoute != DeclarationRouteKind.Interface
                || route.Declarations.Any(declaration =>
                    declaration.Declaration.EventMap.IsEventMap)
                || route.Symbol.Semantic.ExposedOnWorker
                    && !route.Symbol.Semantic.ExposedOnWindow
                    && route.Symbol.Semantic.Exposures.Count > 0)
            {
                continue;
            }

            var logical = logicalEmitter.Emit(route.Symbol);
            var server = serverTransformer.Transform(route.Symbol, logical.Source);
            var wasm = wasmTransformer.Transform(route.Symbol, logical.Source);
            var relativeDirectory = Naming.ToOutputSubdirectory(
                "Interfaces",
                route.Symbol.Name);
            var fileStem =
                $"I{Naming.ToCSharpSimpleTypeName(route.Symbol.Name)}";
            serverFiles.Add(writer.Write(
                fileStem,
                server.Source,
                Path.Combine("Server", relativeDirectory)));
            wasmFiles.Add(writer.Write(
                fileStem,
                wasm.Source,
                Path.Combine("WebAssembly", relativeDirectory)));
            serverOperations.AddRange(server.Operations);
            wasmOperations.AddRange(wasm.Operations);
            hostSymbols.Add(route.Symbol.Name);
            proxyRegistrations.Add(new ProxyRegistration(
                Naming.ToGeneratedNamespace(
                    GenerationPipeline.GeneratedNamespace,
                    route.Symbol.Name),
                server.ContractType,
                server.ProxyType));
        }

        var factoryRoutes = routing.SupplementalDeclarations
            .Where(route => route.Route == DeclarationRouteKind.FactoryConstructor)
            .GroupBy(route => route.Symbol.Name, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(route => route.Symbol.Ordinal)
            .ToList();
        var serverFactoryTransformer =
            new HostFactoryTransformer(DomHostKind.Server);
        var wasmFactoryTransformer =
            new HostFactoryTransformer(DomHostKind.WebAssembly);
        var rootFactories = new List<RootFactory>();
        foreach (var route in factoryRoutes)
        {
            var fileStem =
                $"I{Naming.ToCSharpSimpleTypeName(route.Symbol.Name)}Factory";
            var logicalFile = writer.WrittenFiles.FirstOrDefault(file =>
                string.Equals(
                    file.CSharpTypeName,
                    fileStem,
                    StringComparison.Ordinal)
                && file.RelativePath.Replace('\\', '/').StartsWith(
                    "Factories/",
                    StringComparison.Ordinal));
            if (logicalFile is null
                || !writer.TryGetSource(logicalFile.RelativePath, out var source))
            {
                continue;
            }

            var constructorPath = route.Symbol.Name;
            var serverFactory = serverFactoryTransformer.Transform(
                route.Symbol,
                source,
                constructorPath);
            var wasmFactory = wasmFactoryTransformer.Transform(
                route.Symbol,
                source,
                constructorPath);
            var logicalDirectory =
                Path.GetDirectoryName(logicalFile.RelativePath) ?? "Factories";
            var nested = Path.GetRelativePath("Factories", logicalDirectory);
            var serverDirectory = nested == "."
                ? Path.Combine("Server", "Factories")
                : Path.Combine("Server", "Factories", nested);
            var wasmDirectory = nested == "."
                ? Path.Combine("WebAssembly", "Factories")
                : Path.Combine("WebAssembly", "Factories", nested);
            serverFiles.Add(writer.Write(
                fileStem,
                serverFactory.Source,
                serverDirectory));
            wasmFiles.Add(writer.Write(
                fileStem,
                wasmFactory.Source,
                wasmDirectory));
            serverOperations.AddRange(serverFactory.Operations);
            wasmOperations.AddRange(wasmFactory.Operations);
            var generatedNamespace = Naming.ToGeneratedNamespace(
                GenerationPipeline.GeneratedNamespace,
                route.Symbol.Name);
            proxyRegistrations.Add(new ProxyRegistration(
                generatedNamespace,
                serverFactory.ContractType,
                serverFactory.ProxyType));
            if (route.TypeScriptNamespace is null)
            {
                rootFactories.Add(new RootFactory(
                    route.Symbol.Name,
                    generatedNamespace,
                    serverFactory.ContractType,
                    Naming.ToCSharpMemberName(
                        Naming.ToCSharpSimpleTypeName(route.Symbol.Name))
                        + "Constructor"));
            }
        }

        if (hostSymbols.Contains("Window", StringComparer.Ordinal))
        {
            var serverFactoryNavigation = EmitFactoryNavigation(
                DomHostKind.Server,
                rootFactories);
            var wasmFactoryNavigation = EmitFactoryNavigation(
                DomHostKind.WebAssembly,
                rootFactories);
            serverFiles.Add(writer.Write(
                "IWindow.Factories",
                serverFactoryNavigation.Source,
                Path.Combine("Server", "Factories")));
            wasmFiles.Add(writer.Write(
                "IWindow.Factories",
                wasmFactoryNavigation.Source,
                Path.Combine("WebAssembly", "Factories")));
            serverOperations.AddRange(serverFactoryNavigation.Operations);
            wasmOperations.AddRange(wasmFactoryNavigation.Operations);
        }

        var serverInfrastructure = EmitInfrastructure(
            DomHostKind.Server,
            proxyRegistrations,
            rootFactories,
            options.Capability,
            options.EmitCapabilityFacade);
        var wasmInfrastructure = EmitInfrastructure(
            DomHostKind.WebAssembly,
            proxyRegistrations,
            rootFactories,
            options.Capability,
            options.EmitCapabilityFacade);
        serverFiles.Add(writer.Write(
            "GeneratedDomHost",
            serverInfrastructure.Source,
            "Server"));
        wasmFiles.Add(writer.Write(
            "GeneratedDomHost",
            wasmInfrastructure.Source,
            "WebAssembly"));
        serverOperations.AddRange(serverInfrastructure.Operations);
        wasmOperations.AddRange(wasmInfrastructure.Operations);

        var hostSymbolSet = hostSymbols.ToHashSet(StringComparer.Ordinal);
        var sharedSymbols = ir.TypescriptSymbols
            .Select(symbol => symbol.Name)
            .Where(symbol => !hostSymbolSet.Contains(symbol))
            .Order(StringComparer.Ordinal)
            .ToList();
        hostSymbols.Sort(StringComparer.Ordinal);

        var serverManifest = new HostApiManifest(
            SchemaVersion: 1,
            GenerationPipeline.GeneratorVersion,
            DomHostKind.Server,
            ir.TypescriptSymbols.Count,
            sharedSymbols,
            hostSymbols,
            serverOperations
                .OrderBy(operation => operation.LogicalIdentity, StringComparer.Ordinal)
                .ToList(),
            serverFiles.Order(StringComparer.Ordinal).ToList(),
            options.EmitCapabilityFacade ? options.Capability : null);
        var wasmManifest = new HostApiManifest(
            SchemaVersion: 1,
            GenerationPipeline.GeneratorVersion,
            DomHostKind.WebAssembly,
            ir.TypescriptSymbols.Count,
            sharedSymbols,
            hostSymbols,
            wasmOperations
                .OrderBy(operation => operation.LogicalIdentity, StringComparer.Ordinal)
                .ToList(),
            wasmFiles.Order(StringComparer.Ordinal).ToList(),
            options.EmitCapabilityFacade ? options.Capability : null);
        serverManifest.Validate();
        wasmManifest.Validate();
        var parity = HostParityReport.Compare(serverManifest, wasmManifest);
        if (!parity.Exact)
        {
            throw new InvalidOperationException(
                $"Host API parity failed with {parity.UnexplainedDeltas.Count} " +
                "unexplained delta(s).");
        }

        writer.WriteManifest(serverManifest, Path.Combine("Server", "host-manifest.json"));
        writer.WriteManifest(wasmManifest, Path.Combine("WebAssembly", "host-manifest.json"));
        writer.WriteManifest(parity, "host-parity.json");
        return new HostPackageGenerationResult(serverManifest, wasmManifest, parity);
    }

    private static InfrastructureResult EmitInfrastructure(
        DomHostKind host,
        IReadOnlyList<ProxyRegistration> registrations,
        IReadOnlyList<RootFactory> rootFactories,
        HostCapabilityMetadata capability,
        bool emitCapabilityFacade)
    {
        var writer = new CSharpWriter();
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            GenerationPipeline.GeneratorVersion));
        if (emitCapabilityFacade)
        {
            writer.AppendLine(
                "using global::Microsoft.Extensions.DependencyInjection;");
            writer.AppendLine();
        }
        writer.AppendLine("namespace Microsoft.JSInterop;");
        writer.AppendLine();
        writer.Block(
            "public static class GeneratedDomHost",
            () =>
            {
                foreach (var entry in capability.EntryPoints)
                {
                    var type = ResolveEntryPointContract(entry, rootFactories);
                    var name = Naming.ToCSharpMemberName(entry.Name);
                    writer.AppendLine(
                        "public static global::System.Threading.Tasks.ValueTask<" +
                        $"{type.QualifiedName}> Get{name}ProxyAsync(");
                    writer.AppendLine(
                        "    this IBrowser browser, global::System.Threading." +
                        "CancellationToken cancellationToken = default) =>");
                    writer.AppendLine(
                        $"    browser.GetGlobalAsync<{type.QualifiedName}>(" +
                        $"\"{entry.JavaScriptPath}\", cancellationToken);");
                    writer.AppendLine();
                }
                writer.Block(
                    "public static void RegisterProxies(IDomProxyFactory factory)",
                    () =>
                    {
                        writer.AppendLine(
                            "global::System.ArgumentNullException.ThrowIfNull(factory);");
                        foreach (var registration in registrations
                            .OrderBy(item => item.Namespace, StringComparer.Ordinal)
                            .ThenBy(item => item.Contract, StringComparer.Ordinal))
                        {
                            EmitRegistration(writer, registration);
                        }
                    });
            });
        if (emitCapabilityFacade)
        {
            writer.AppendLine();
            EmitCapabilityFacade(writer, host, rootFactories, capability);
        }
        var operations = capability.EntryPoints
            .Select(entry =>
            {
                var type = ResolveEntryPointContract(entry, rootFactories);
                return new HostApiOperation(
                    $"global:{entry.JavaScriptPath}",
                    entry.Symbol,
                    type.IsFactory ? "constructor-global" : "global",
                    entry.JavaScriptPath,
                    false,
                    $"ValueTask<{type.DisplayName}> " +
                    $"Get{Naming.ToCSharpMemberName(entry.Name)}ProxyAsync(CancellationToken)");
            })
            .ToList();
        return new InfrastructureResult(writer.ToString(), operations);
    }

    private static void EmitCapabilityFacade(
        CSharpWriter writer,
        DomHostKind host,
        IReadOnlyList<RootFactory> rootFactories,
        HostCapabilityMetadata capability)
    {
        var capabilityName = Naming.ToCSharpSimpleTypeName(capability.Name);
        writer.Block(
            $"public interface I{capabilityName}Capability",
            () =>
            {
                foreach (var entry in capability.EntryPoints)
                {
                    var type = ResolveEntryPointContract(entry, rootFactories);
                    var name = Naming.ToCSharpMemberName(entry.Name);
                    writer.AppendLine(
                        $"global::System.Threading.Tasks.ValueTask<{type.QualifiedName}> " +
                        $"Get{name}Async(global::System.Threading.CancellationToken " +
                        "cancellationToken = default);");
                    if (host == DomHostKind.WebAssembly)
                    {
                        writer.AppendLine(
                            $"{type.QualifiedName} Get{name}();");
                    }
                }
            });
        writer.AppendLine();
        var dependencies = host == DomHostKind.Server
            ? "IBrowser browser"
            : "IBrowser browser, IDomProxyFactory proxyFactory, IDomRuntime runtime";
        writer.Block(
            $"internal sealed class {capabilityName}Capability({dependencies}) : " +
            $"I{capabilityName}Capability",
            () =>
            {
                foreach (var entry in capability.EntryPoints)
                {
                    var type = ResolveEntryPointContract(entry, rootFactories);
                    var name = Naming.ToCSharpMemberName(entry.Name);
                    writer.AppendLine(
                        $"public global::System.Threading.Tasks.ValueTask<{type.QualifiedName}> " +
                        $"Get{name}Async(global::System.Threading.CancellationToken " +
                        "cancellationToken = default) =>");
                    writer.AppendLine(
                        $"    browser.GetGlobalAsync<{type.QualifiedName}>(" +
                        $"\"{entry.JavaScriptPath}\", cancellationToken);");
                    if (host == DomHostKind.WebAssembly)
                    {
                        writer.AppendLine();
                        writer.AppendLine(
                            $"public {type.QualifiedName} Get{name}() =>");
                        writer.AppendLine(
                            $"    proxyFactory.Create<{type.QualifiedName}>(" +
                            "((IDomSyncRuntime)runtime).GetGlobalRef(" +
                            $"\"{entry.JavaScriptPath}\"));");
                    }
                    writer.AppendLine();
                }
            });
        writer.AppendLine();
        writer.Block(
            $"public static class {capabilityName}CapabilityMetadata",
            () =>
            {
                writer.AppendLine(
                    $"public const bool RequiresSecureContext = " +
                    $"{capability.SecureContext.ToString().ToLowerInvariant()};");
                writer.AppendLine(
                    $"public const bool RequiresUserActivation = " +
                    $"{capability.RequiresUserActivation.ToString().ToLowerInvariant()};");
                writer.AppendLine(
                    "public static global::System.Collections.Generic." +
                    "IReadOnlyList<string> Features { get; } =");
                writer.AppendLine(
                    $"    [{string.Join(", ", capability.Features.Select(value => $"\"{value}\""))}];");
                writer.AppendLine(
                    "public static global::System.Collections.Generic." +
                    "IReadOnlyList<string> Permissions { get; } =");
                writer.AppendLine(
                    $"    [{string.Join(", ", (capability.Permissions ?? []).Select(value => $"\"{value}\""))}];");
                writer.AppendLine(
                    "public static global::System.Collections.Generic." +
                    "IReadOnlyList<string> FeatureDetectionPaths { get; } =");
                writer.AppendLine(
                    $"    [{string.Join(", ", capability.EntryPoints.Select(value => $"\"{value.JavaScriptPath}\""))}];");
            });
        writer.AppendLine();
        writer.Block(
            $"public static class {capabilityName}CapabilityServiceCollectionExtensions",
            () =>
            {
                writer.AppendLine(
                    "public static global::Microsoft.Extensions.DependencyInjection." +
                    $"IServiceCollection Add{capabilityName}Capability(");
                writer.AppendLine(
                    "    this global::Microsoft.Extensions.DependencyInjection." +
                    "IServiceCollection services)");
                writer.AppendLine("{");
                writer.AppendLine(
                    "    global::System.ArgumentNullException.ThrowIfNull(services);");
                var runtimeType = host == DomHostKind.Server
                    ? "ServerDomRuntime"
                    : "WasmDomRuntime";
                writer.AppendLine(
                    $"    services.AddScoped<{runtimeType}>();");
                writer.AppendLine(
                    "    services.AddScoped<IDomRuntime>(static provider => " +
                    $"provider.GetRequiredService<{runtimeType}>());");
                if (host == DomHostKind.WebAssembly)
                {
                    writer.AppendLine(
                        "    services.AddScoped<IDomSyncRuntime>(static provider => " +
                        $"provider.GetRequiredService<{runtimeType}>());");
                }
                writer.AppendLine(
                    "    services.AddScoped<IDomProxyFactory>(static provider =>");
                writer.AppendLine("    {");
                writer.AppendLine(
                    "        var factory = new DomProxyFactory(" +
                    "provider.GetRequiredService<IDomRuntime>());");
                writer.AppendLine("        GeneratedDomHost.RegisterProxies(factory);");
                writer.AppendLine("        return factory;");
                writer.AppendLine("    });");
                var browserType = host == DomHostKind.Server
                    ? "ServerBrowser"
                    : "WasmBrowser";
                writer.AppendLine(
                    $"    services.AddScoped<IBrowser, {browserType}>();");
                writer.AppendLine(
                    $"    services.AddScoped<I{capabilityName}Capability, " +
                    $"{capabilityName}Capability>();");
                writer.AppendLine("    return services;");
                writer.AppendLine("}");
            });
    }

    private static EntryPointContract ResolveEntryPointContract(
        HostEntryPoint entry,
        IReadOnlyList<RootFactory> rootFactories)
    {
        var factory = rootFactories.SingleOrDefault(candidate =>
            string.Equals(
                candidate.JavaScriptName,
                entry.JavaScriptPath,
                StringComparison.Ordinal)
            && string.Equals(
                candidate.JavaScriptName,
                entry.Symbol,
                StringComparison.Ordinal));
        if (factory is not null)
        {
            return new EntryPointContract(
                $"global::{factory.Namespace}.{factory.ContractType}",
                factory.ContractType,
                IsFactory: true);
        }

        var type = Naming.ToCSharpSimpleTypeName(entry.Symbol);
        return new EntryPointContract(
            $"global::Blazor.DOM.I{type}",
            $"I{type}",
            IsFactory: false);
    }

    private static void ValidateEntryPoints(
        IrBundle ir,
        HostCapabilityMetadata capability)
    {
        var symbols = ir.TypescriptSymbols.ToDictionary(
            symbol => symbol.Name,
            StringComparer.Ordinal);
        foreach (var entry in capability.EntryPoints)
        {
            if (!symbols.TryGetValue(entry.Symbol, out var symbol))
            {
                throw new InvalidOperationException(
                    $"Host entry point '{entry.Name}' references missing symbol " +
                    $"'{entry.Symbol}'.");
            }
            if (string.Equals(
                symbol.Semantic.Status,
                "ambiguous",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Host entry point '{entry.Name}' references ambiguous symbol " +
                    $"'{entry.Symbol}'.");
            }
        }
    }

    private static InfrastructureResult EmitFactoryNavigation(
        DomHostKind host,
        IReadOnlyList<RootFactory> factories)
    {
        var writer = new CSharpWriter();
        writer.AppendLine(CSharpWriter.AutoGeneratedHeader(
            "Blazor.DOM.CSharpGenerator",
            GenerationPipeline.GeneratorVersion));
        writer.AppendLine("namespace Blazor.DOM;");
        writer.AppendLine();
        writer.Block(
            "public partial interface IWindow",
            () =>
            {
                foreach (var factory in factories
                    .OrderBy(item => item.AccessorName, StringComparer.Ordinal))
                {
                    var contract =
                        $"global::{factory.Namespace}.{factory.ContractType}";
                    if (host == DomHostKind.Server)
                    {
                        writer.AppendLine(
                            $"global::System.Threading.Tasks.ValueTask<{contract}> " +
                            $"Get{factory.AccessorName}Async(" +
                            "global::System.Threading.CancellationToken " +
                            "cancellationToken = default) =>");
                        writer.AppendLine(
                            "    global::Microsoft.JSInterop.DomDispatch." +
                            $"GetPropertyAsync<{contract}>(" +
                            "(global::Microsoft.JSInterop.IDomDispatchProxy)this, " +
                            $"\"{factory.JavaScriptName}\", " +
                            $"global::Microsoft.JSInterop.DomTransportDescriptor." +
                            $"JsReference(\"{factory.JavaScriptName}\"), " +
                            "cancellationToken);");
                    }
                    else
                    {
                        writer.AppendLine(
                            $"{contract} {factory.AccessorName} => " +
                            "global::Microsoft.JSInterop.WasmDomDispatch." +
                            $"GetProperty<{contract}>(" +
                            "(global::Microsoft.JSInterop.IDomDispatchProxy)this, " +
                            $"\"{factory.JavaScriptName}\", " +
                            $"global::Microsoft.JSInterop.DomTransportDescriptor." +
                            $"JsReference(\"{factory.JavaScriptName}\"));");
                    }
                    writer.AppendLine();
                }
            });
        var operations = factories.Select(factory =>
            new HostApiOperation(
                $"Window/factory:{factory.JavaScriptName}",
                "Window",
                "factory-navigation",
                factory.JavaScriptName,
                false,
                $"{factory.ContractType} {factory.AccessorName}"))
            .ToList();
        return new InfrastructureResult(writer.ToString(), operations);
    }

    private static void EmitRegistration(
        CSharpWriter writer,
        ProxyRegistration registration)
    {
        var contract = $"global::{registration.Namespace}.{registration.Contract}";
        var proxy = $"global::{registration.Namespace}.{registration.Proxy}";
        if (!registration.Contract.Contains('<', StringComparison.Ordinal))
        {
            writer.AppendLine(
                $"factory.Register<{contract}>((reference, runtime, owner) => " +
                $"new {proxy}(reference, runtime, owner));");
            return;
        }

        writer.AppendLine(
            $"factory.RegisterOpenGeneric(typeof({OpenGeneric(contract)}), " +
            $"typeof({OpenGeneric(proxy)}));");
    }

    private static string OpenGeneric(string type)
    {
        var open = type.IndexOf('<');
        var arguments = type[(open + 1)..^1];
        var arity = 1;
        var depth = 0;
        foreach (var character in arguments)
        {
            if (character == '<') depth++;
            else if (character == '>') depth--;
            else if (character == ',' && depth == 0) arity++;
        }
        return type[..open] + "<" + new string(',', arity - 1) + ">";
    }

    private sealed record ProxyRegistration(
        string Namespace,
        string Contract,
        string Proxy);

    private sealed record RootFactory(
        string JavaScriptName,
        string Namespace,
        string ContractType,
        string AccessorName);

    private sealed record EntryPointContract(
        string QualifiedName,
        string DisplayName,
        bool IsFactory);

    private sealed record InfrastructureResult(
        string Source,
        IReadOnlyList<HostApiOperation> Operations);
}
