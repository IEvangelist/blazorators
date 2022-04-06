# Blazorators: The Source Generated `speechRecognition` JavaScript Interop library for Blazor WebAssembly

The [`Blazor.SpeechRecognition.WebAssembly`](https://www.nuget.org/packages/Blazor.SpeechRecognition.WebAssembly) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `ISpeechRecognition` interface specific to Blazor WebAssembly and the [`speechRecognition`](https://developer.mozilla.org/docs/Web/API/Window/speechRecognition) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSpeechRecognitionServices` method to register the `ISpeechRecognitionService` type.

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

builder.Services.AddSpeechRecognitionServices();

await builder.Build().RunAsync();
```

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `ISpeechRecognitionService` type. The interface takes the following shape:

```csharp
namespace Microsoft.JSInterop;

/// <summary>
/// A service the exposes various JavaScript interop capabilities specific to the
/// <c>speechRecognition</c> APIs. See <a href="https://developer.mozilla.org/docs/Web/API/SpeechRecognition"></a>
/// </summary>
public interface ISpeechRecognitionService : IAsyncDisposable
{
    /// <summary>
    /// Call once, before using in the consuming components
    /// <c>OnAfterRenderAsync(bool firstRender)</c> override, when firstRender is <c>true</c>.
    /// </summary>
    Task InitializeModuleAsync();

    /// <summary>
    /// Cancels the active speech recognition session.
    /// </summary>
    /// <param name="isAborted">
    /// Is aborted controls which API to call,
    /// either <c>speechRecognition.stop</c> or <c>speechRecognition.abort</c>.
    /// </param>
    void CancelSpeechRecognition(bool isAborted);

    /// <summary>
    /// Starts the speech recognition process. Returns an <see cref="IDisposable"/>
    /// that acts as the subscription. The various callbacks are invoked as they occur,
    /// and will continue to fire until the subscription is disposed of.
    /// </summary>
    /// <param name="language">The BCP47 language tag.</param>
    /// <param name="onRecognized">The callback to invoke when <c>onrecognized</c> fires.</param>
    /// <param name="onError">The optional callback to invoke when <c>onerror</c> fires.</param>
    /// <param name="onStarted">The optional callback to invoke when <c>onstarted</c> fires.</param>
    /// <param name="onEnded">The optional callback to invoke when <c>onended</c> fires.</param>
    /// <returns>
    /// To unsubscribe from the speech recognition, call
    /// <see cref="IDisposable.Dispose"/>.
    /// </returns>
    IDisposable RecognizeSpeech(
        string language,
        Action<string> onRecognized,
        Action<SpeechRecognitionErrorEvent>? onError = null,
        Action? onStarted = null,
        Action? onEnded = null);
}
```

### Initialize the module

In the consuming component, call `ISpeechRecognitionService.InitializeModuleAsync` in the `OnAfterRenderAsync` override:

```csharp
public partial class ExampleConsumingComponent
{
    [Inject]
    public ISpeechRecognitionService SpeechRecognition { get; set; } = null!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SpeechRecognition.InitializeModuleAsync();
        }
    }

    // Omitted for brevity...
}
```