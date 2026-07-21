using System.Reflection;
using System.Text.Json;
using Blazor.DOM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blazor.FileSystemAccess.Tests;

public sealed class FileSystemAccessPackageTests
{
    [Fact]
    public void RegistrationAddsScopedCapabilityAndRuntime()
    {
        var services = new ServiceCollection();

        var returned = services.AddFileSystemAccessCapability();

        Assert.Same(services, returned);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IFileSystemAccessCapability)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IDomProxyFactory)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void WindowExposesOnlyExplicitUserActivatedPickerOperations()
    {
        var root = typeof(IFileSystemAccessCapability).GetMethod("GetWindowAsync");
        Assert.Equal(typeof(ValueTask<IWindow>), root?.ReturnType);
        Assert.True(FileSystemAccessCapabilityMetadata.RequiresSecureContext);
        Assert.True(FileSystemAccessCapabilityMetadata.RequiresUserActivation);
        Assert.Equal(
            ["file-system-access", "local-file-system"],
            FileSystemAccessCapabilityMetadata.Features);
        Assert.Equal(["window"], FileSystemAccessCapabilityMetadata.FeatureDetectionPaths);

        var methods = typeof(IWindow).GetMethods()
            .Where(method => method.GetCustomAttribute<DomOperationAttribute>() is not null)
            .ToDictionary(method => method.Name);
        Assert.Equal(
            ["ShowDirectoryPickerAsync", "ShowOpenFilePickerAsync", "ShowSaveFilePickerAsync"],
            methods.Keys.Order());
        Assert.Equal(
            typeof(ValueTask<IBrowserArray<IFileSystemFileHandle>>),
            methods["ShowOpenFilePickerAsync"].ReturnType);
        Assert.Equal(
            typeof(ValueTask<IFileSystemFileHandle>),
            methods["ShowSaveFilePickerAsync"].ReturnType);
        Assert.Equal(
            typeof(ValueTask<IFileSystemDirectoryHandle>),
            methods["ShowDirectoryPickerAsync"].ReturnType);
        Assert.All(methods.Values, method =>
        {
            Assert.True(method.GetCustomAttribute<DomOperationAttribute>()?.Promise);
            Assert.Equal(
                DomTransportKind.JsReference,
                method.GetCustomAttribute<DomOperationAttribute>()?.ReturnTransport);
        });
    }

    [Fact]
    public void HandlesRemainDisposableProxiesWithPromiseOperations()
    {
        Type[] handles =
        [
            typeof(IFileSystemHandle),
            typeof(IFileSystemFileHandle),
            typeof(IFileSystemDirectoryHandle),
            typeof(IFile),
        ];
        Assert.All(handles, handle =>
        {
            Assert.True(typeof(IDomDispatchProxy).IsAssignableFrom(handle));
            Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(handle));
        });

        AssertOperation(
            typeof(IFileSystemHandle).GetMethod("IsSameEntryAsync"),
            DomTransportKind.JsonValue);
        AssertOperation(
            typeof(IFileSystemHandle).GetMethod("QueryPermissionAsync"),
            DomTransportKind.JsonValue);
        AssertOperation(
            typeof(IFileSystemHandle).GetMethod("RequestPermissionAsync"),
            DomTransportKind.JsonValue);
        AssertOperation(
            typeof(IFileSystemFileHandle).GetMethod("GetFileAsync"),
            DomTransportKind.JsReference);
        Assert.Null(typeof(IFileSystemFileHandle).GetMethod("CreateWritableAsync"));
    }

    [Fact]
    public void DirectoryTraversalPreservesArraysAndAsyncIteratorReferences()
    {
        AssertOperation(
            typeof(IFileSystemDirectoryHandle).GetMethod("GetDirectoryHandleAsync"),
            DomTransportKind.JsReference);
        AssertOperation(
            typeof(IFileSystemDirectoryHandle).GetMethod("GetFileHandleAsync"),
            DomTransportKind.JsReference);
        AssertOperation(
            typeof(IFileSystemDirectoryHandle).GetMethod("RemoveEntryAsync"),
            DomTransportKind.JsonValue);

        var resolve = typeof(IFileSystemDirectoryHandle).GetMethod("ResolveAsync");
        Assert.Equal(typeof(ValueTask<string[]?>), resolve?.ReturnType);

        var entries = typeof(IFileSystemDirectoryHandle).GetMethod("EntriesAsync");
        var keys = typeof(IFileSystemDirectoryHandle).GetMethod("KeysAsync");
        var values = typeof(IFileSystemDirectoryHandle).GetMethod("ValuesAsync");
        Assert.NotNull(entries);
        Assert.NotNull(keys);
        Assert.NotNull(values);
        Assert.Equal(DomTransportKind.JsReference, Operation(entries).ReturnTransport);
        Assert.Equal(DomTransportKind.JsReference, Operation(keys).ReturnTransport);
        Assert.Equal(DomTransportKind.JsReference, Operation(values).ReturnTransport);
        Assert.True(typeof(IAsyncDisposable).IsAssignableFrom(
            entries.ReturnType.GenericTypeArguments.Single()));
    }

    [Fact]
    public void FileReadsPreserveBinaryTransportAndTypedOptions()
    {
        var arrayBuffer = typeof(IBlob).GetMethod("ArrayBufferAsync");
        var bytes = typeof(IBlob).GetMethod("BytesAsync");
        var text = typeof(IBlob).GetMethod("TextAsync");
        Assert.Equal(typeof(ValueTask<byte[]>), arrayBuffer?.ReturnType);
        Assert.Equal(typeof(ValueTask<byte[]>), bytes?.ReturnType);
        Assert.Equal(DomTransportKind.Binary, Operation(arrayBuffer).ReturnTransport);
        Assert.Equal(DomTransportKind.Binary, Operation(bytes).ReturnTransport);
        Assert.Equal(typeof(ValueTask<string>), text?.ReturnType);

        var accept = typeof(FilePickerAcceptType).GetProperty("Accept")?.PropertyType;
        Assert.NotNull(accept);
        Assert.Equal(typeof(IReadOnlyDictionary<,>), accept.GetGenericTypeDefinition());
        Assert.Equal(typeof(string), accept.GenericTypeArguments[0]);
        Assert.True(typeof(IDomUnionValue).IsAssignableFrom(accept.GenericTypeArguments[1]));
        Assert.Null(typeof(FilePickerOptions).GetProperty("StartIn"));
        Assert.Null(typeof(DirectoryPickerOptions).GetProperty("StartIn"));
        Assert.Equal(
            typeof(FileSystemPermissionMode?),
            typeof(FileSystemHandlePermissionDescriptor).GetProperty("Mode")?.PropertyType);
        Assert.Equal(
            typeof(bool?),
            typeof(FileSystemRemoveOptions).GetProperty("Recursive")?.PropertyType);
    }

    [Fact]
    public void GeneratedManifestsHaveExactParityCoverageAndReviewedExclusions()
    {
        using var parity = ReadManifest("host-parity.json");
        Assert.True(parity.RootElement.GetProperty("exact").GetBoolean());
        Assert.Equal(52, parity.RootElement.GetProperty("serverOperationCount").GetInt32());
        Assert.Equal(
            52,
            parity.RootElement.GetProperty("webAssemblyOperationCount").GetInt32());
        Assert.Empty(parity.RootElement.GetProperty("unexplainedDeltas").EnumerateArray());

        using var coverage = ReadManifest("profile-coverage.json");
        Assert.True(coverage.RootElement.GetProperty("byteIdentityVerified").GetBoolean());
        Assert.Equal(38, coverage.RootElement.GetProperty("closureSize").GetInt32());
        Assert.Equal(30, coverage.RootElement.GetProperty("includedSymbolCount").GetInt32());
        Assert.Equal(8, coverage.RootElement.GetProperty("externalReferenceCount").GetInt32());
        Assert.Equal(
            [
                "Array",
                "ArrayBuffer",
                "ArrayBufferView",
                "AsyncIteratorObject",
                "BuiltinIteratorReturn",
                "Promise",
                "Record",
                "Uint8Array",
            ],
            coverage.RootElement.GetProperty("externalReferences")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.Equal(
            5,
            coverage.RootElement.GetProperty("transportOverrides").GetArrayLength());
        Assert.Equal(
            3,
            coverage.RootElement.GetProperty("reviewedExclusions").GetArrayLength());
        var accounting = coverage.RootElement.GetProperty("accounting");
        Assert.Equal(56, accounting.GetProperty("projectedMembers").GetInt32());
        Assert.Equal(0, accounting.GetProperty("deferredMembers").GetInt32());
        Assert.Equal(0, accounting.GetProperty("failedMembers").GetInt32());

        using var server = ReadManifest("Server", "host-manifest.json");
        Assert.Equal(52, server.RootElement.GetProperty("operations").GetArrayLength());
        Assert.DoesNotContain(
            server.RootElement.GetProperty("operations").EnumerateArray(),
            operation => operation.GetProperty("kind").GetString()
                ?.Contains("unsupported", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static void AssertOperation(MethodInfo? method, DomTransportKind transport)
    {
        Assert.NotNull(method);
        var operation = Operation(method);
        Assert.True(operation.Promise);
        Assert.Equal(transport, operation.ReturnTransport);
    }

    private static DomOperationAttribute Operation(MethodInfo? method)
        => Assert.IsType<DomOperationAttribute>(
            method?.GetCustomAttribute<DomOperationAttribute>());

    private static JsonDocument ReadManifest(params string[] relativePath)
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "artifacts",
            "obj",
            "Blazor.DOM.Generation",
            "release",
            "dom",
            "Profiles",
            "FileSystemAccess",
            Path.Combine(relativePath));
        Assert.True(File.Exists(path), $"Missing generated manifest: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
