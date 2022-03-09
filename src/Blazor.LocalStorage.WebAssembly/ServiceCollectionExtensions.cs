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
    /// The <see cref="IJSInProcessRuntime"/> is available as a singleton.
    /// <a href="http://tiny.cc/lb-js-di-lifetime"></a>
    /// </summary>
    public static IServiceCollection AddInProcessJavaScript(
        this IServiceCollection services) =>
        services.AddSingleton<IJSInProcessRuntime>(
            serviceProvider =>
                (IJSInProcessRuntime)serviceProvider.GetRequiredService<IJSRuntime>());
}
