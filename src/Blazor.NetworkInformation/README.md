# Blazorators: The Source Generated `NetworkInformation` JavaScript Interop library for Blazor Server

The [`Blazor.NetworkInformation`](https://www.nuget.org/packages/Blazor.NetworkInformation) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `INetworkInformation` interface specific to Blazor WebAssembly and the [`localStorage`](https://developer.mozilla.org/docs/Web/API/Window/localStorage) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddConnectionServices` method to register the `IStorageService` service type.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddConnectionServices();
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

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IStorageService` type. The interface takes the following shape:

```csharp
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>NetworkInformation</c> type.
/// </summary>
public partial interface INetworkInformationService
{
    /// <summary>
    /// Source generated implementation of <c>window.navigator.connection.type</c>.
    /// <a href="https://developer.mozilla.org/docs/Web/API/NetworkInformation/type"></a>
    /// </summary>
    ValueTask<string> Type { get; }
}
```
