# Blazorators: The Source Generated `speechSynthesis` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.SpeechSynthesis.WebAssembly`](https://www.nuget.org/packages/Blazor.SpeechSynthesis.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `ISpeechSynthesis` interface specific to Blazor WebAssembly and the [`speechSynthesis`](https://developer.mozilla.org/docs/Web/API/Window/speechSynthesis) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSpeechSynthesisServices` method to register the `IStorageService` service type.

```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(
    sp => new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });

builder.Services.AddSpeechSynthesisServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IStorageService` type. The interface takes the following shape:

```csharp
using Blazor.Serialization.Extensions;
using System.Text.Json;

#nullable enable

namespace Microsoft.JSInterop;
/// <summary>
/// Source generated interface definition of the <c>SpeechSynthesis</c> type.
/// </summary>
public partial interface ISpeechSynthesisService
{
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.cancel</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/cancel"></a>
    /// </summary>
    void Cancel();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.getVoices</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/getVoices"></a>
    /// </summary>
    SpeechSynthesisVoice[] GetVoices();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.pause</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/pause"></a>
    /// </summary>
    void Pause();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.resume</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/resume"></a>
    /// </summary>
    void Resume();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.paused</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/paused"></a>
    /// </summary>
    bool Paused { get; }

    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.pending</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/pending"></a>
    /// </summary>
    bool Pending { get; }

    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.speaking</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/speaking"></a>
    /// </summary>
    bool Speaking { get; }
}
```
