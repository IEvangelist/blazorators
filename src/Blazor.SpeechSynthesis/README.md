# Blazorators: The Source Generated `speechSynthesis` JavaScript Interop library for Blazor

The [`Blazor.SpeechSynthesis`](https://www.nuget.org/packages/Blazor.SpeechSynthesis) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `ISpeechSynthesis` interface specific to Blazor WebAssembly and the [`speechSynthesis`](https://developer.mozilla.org/docs/Web/API/Window/speechSynthesis) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddSpeechSynthesisServices` method to register the `IStorageService` service type.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSpeechSynthesisServices();
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

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `ISpeechSynthesisService` type. The interface takes the following shape:

```csharp
using System.Threading.Tasks;

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
    ValueTask CancelAsync();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.getVoices</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/getVoices"></a>
    /// </summary>
    SpeechSynthesisVoice[] GetVoicesAsync();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.pause</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/pause"></a>
    /// </summary>
    ValueTask PauseAsync();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.resume</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/resume"></a>
    /// </summary>
    ValueTask ResumeAsync();
    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.paused</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/paused"></a>
    /// </summary>
    ValueTask<bool> Paused { get; }

    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.pending</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/pending"></a>
    /// </summary>
    ValueTask<bool> Pending { get; }

    /// <summary>
    /// Source generated implementation of <c>window.speechSynthesis.speaking</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/SpeechSynthesis/speaking"></a>
    /// </summary>
    ValueTask<bool> Speaking { get; }
}
```

### Add JavaScript dependency

In the *_Host.cshtml* file, add the following:

```html
<script src="_content/Blazor.SpeechSynthesis/blazorators.speechSynthesis.g.js"></script>
```