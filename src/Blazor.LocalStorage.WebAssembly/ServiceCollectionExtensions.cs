// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for configurin the localStorage API for JavaScript
/// interop with the <c>IJSInProcessRuntime</c> WebAssembly-specific type.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ability to either <c>@inject</c>
    /// (or <c>[Inject]</c>) the <c>IJSInProcessRuntime</c> type.
    /// </summary>
    [Obsolete("Call AddInProcessJavaScript instead.")]
    public static IServiceCollection AddWebAssemblyLocalStorage(
        this IServiceCollection services) =>
        services.AddInProcessJavaScript();

    /// <summary>
    /// Adds the ability to either <c>@inject</c>
    /// (or <c>[Inject]</c>) the <c>IJSInProcessRuntime</c> type.
    /// </summary>
    public static IServiceCollection AddInProcessJavaScript(
        this IServiceCollection services) =>
        services.AddScoped<IJSInProcessRuntime>(
            serviceProvider =>
                (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>());
}
