// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Blazor DOM runtime services for Blazor
/// WebAssembly (in-process JS interop with synchronous dispatch paths).
/// </summary>
public static class DomWebAssemblyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IDomSyncRuntime"/>, <see cref="IDomRuntime"/>,
    /// <see cref="IDomProxyFactory"/>, and <see cref="IBrowser"/> services
    /// required for DOM interop on Blazor WebAssembly.
    /// </summary>
    /// <remarks>
    /// All services are registered as scoped.  On Blazor WebAssembly a scoped
    /// lifetime is effectively a singleton for the duration of the session and
    /// avoids DI lifetime mismatch warnings when consumed by transient or scoped
    /// components.
    /// <para>
    /// Do not combine with <c>AddBlazorDOM</c> in the same application; these
    /// registrations are mutually exclusive.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddBlazorDOMWebAssembly(this IServiceCollection services)
    {
        // The WASM runtime implements both IDomSyncRuntime and IDomRuntime.
        // It receives IJSRuntime from DI; on Blazor WebAssembly the registered
        // IJSRuntime is also an IJSInProcessRuntime, but we only depend on
        // the interface we actually use.
        services.AddScoped<WasmDomRuntime>();
        services.AddScoped<IDomRuntime>(
            static sp => sp.GetRequiredService<WasmDomRuntime>());
        services.AddScoped<IDomSyncRuntime>(
            static sp => sp.GetRequiredService<WasmDomRuntime>());

        // Proxy factory — receives IDomRuntime which is actually WasmDomRuntime.
        services.AddScoped<IDomProxyFactory>(
            static sp =>
            {
                var factory = new DomProxyFactory(sp.GetRequiredService<IDomRuntime>());
                global::Microsoft.JSInterop.GeneratedDomHost.RegisterProxies(factory);
                return factory;
            });

        services.AddScoped<IBrowser, WasmBrowser>();

        return services;
    }
}
