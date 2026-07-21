// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Blazor DOM runtime services for Blazor
/// Server (or any hosting model that uses async JS interop).
/// </summary>
public static class DomServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IDomRuntime"/>, <see cref="IDomProxyFactory"/>,
    /// and <see cref="IBrowser"/> services required for DOM interop on Blazor
    /// Server.  All services are scoped to the Blazor circuit lifetime.
    /// </summary>
    /// <remarks>
    /// Do not combine with <c>AddBlazorDOMWebAssembly</c> in the same
    /// application; these registrations are mutually exclusive.
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddBlazorDOM(this IServiceCollection services)
    {
        services.AddScoped<ServerDomRuntime>();
        services.AddScoped<IDomRuntime>(
            static sp => sp.GetRequiredService<ServerDomRuntime>());
        services.AddScoped<IDomProxyFactory>(
            static sp =>
            {
                var factory = new DomProxyFactory(sp.GetRequiredService<IDomRuntime>());
                global::Microsoft.JSInterop.GeneratedDomHost.RegisterProxies(factory);
                return factory;
            });
        services.AddScoped<IBrowser, ServerBrowser>();

        return services;
    }
}
