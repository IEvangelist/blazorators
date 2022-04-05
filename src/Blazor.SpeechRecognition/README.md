# Blazorators: The Source Generated `speechRecognition` JavaScript Interop library for Blazor

The [`Blazor.SpeechRecognition`](https://www.nuget.org/packages/Blazor.SpeechRecognition) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `ISpeechRecognition` interface specific to Blazor and the [`speechRecognition`](https://developer.mozilla.org/docs/Web/API/Window/speechRecognition) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSpeechRecognitionServices` method to register the `ISpeechRecognitionService` type.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSpeechRecognitionServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
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
    /// Cancels the active speech recognition session.
    /// </summary>
    /// <param name="isAborted">
    /// Is aborted controls which API to call,
    /// either <c>speechRecognition.stop</c> or <c>speechRecognition.abort</c>.
    /// </param>
    Task CancelSpeechRecognitionAsync(bool isAborted);

    /// <summary>
    /// Starts the speech recognition process. Callbacks will be invoked on
    /// the <paramref name="component"/> for the given method names.
    /// </summary>
    /// <typeparam name="TComponent">The consuming component (or object).</typeparam>
    /// <param name="component">The calling Razor (or Blazor) component.</param>
    /// <param name="language">The BCP47 language tag.</param>
    /// <param name="onResultCallbackMethodName">
    /// Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the
    /// following <see cref="Func{String, Task}"/> signature.
    /// </param>
    /// <param name="onErrorCallbackMethodName">
    /// Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the
    /// following <see cref="Func{SpeechRecognitionErrorEvent, Task}"/> signature.
    /// </param>
    /// <param name="onStartCallbackMethodName">
    /// Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the
    /// following <see cref="Func{Task}"/>.
    /// </param>
    /// <param name="onEndCallbackMethodName">
    /// Expects the name of a <c>"JSInvokableAttribute"</c> C# method with the
    /// following <see cref="Func{Task}"/> signature.
    /// </param>
    Task RecognizeSpeechAsync<TComponent>(
        TComponent component,
        string language,
        string onResultCallbackMethodName,
        string? onErrorCallbackMethodName = null,
        string? onStartCallbackMethodName = null,
        string? onEndCallbackMethodName = null) where TComponent : class;

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
    Task<IDisposable> RecognizeSpeechAsync(
        string language,
        Func<string, Task> onRecognized,
        Func<SpeechRecognitionErrorEvent, Task>? onError = null,
        Func<Task>? onStarted = null,
        Func<Task>? onEnded = null);
}
```