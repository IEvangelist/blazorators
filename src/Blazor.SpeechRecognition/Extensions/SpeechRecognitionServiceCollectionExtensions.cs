// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions methods for registering the <see cref="ISpeechRecognitionService"/> type.
/// </summary>
public static class SpeechRecognitionServiceCollectionExtensions
{
    /// <summary>
    /// Adds the necessary services for the <see cref="ISpeechRecognitionService"/> to be consumable.
    /// </summary>
    /// <param name="services">The service collection to add additional services to.</param>
    /// <returns>The same <paramref name="services"/> instance that was added to.</returns>
    public static IServiceCollection AddSpeechRecognitionServices(
        this IServiceCollection services) =>
        services.AddSingleton<ISpeechRecognitionService, DefaultSpeechRecognitionService>();
}
