# Blazorators: The Source Generated `geolocation` JavaScript Interop library for Blazor Server

The [`Blazor.Geolocation.Server`](https://www.nuget.org/packages/Blazor.Geolocation.Server) package consumes the [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) package. It exposes a source generated `IGeolocation` interface specific to Blazor WebAssembly and the [`geolocation`](https://developer.mozilla.org/docs/Web/API/Geolocation) Web API.

## Get started

After the NuGet package is added as a reference, call the `AddGeolocationServices` method to register the `IGeolocation` service type.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGeolocationServices();
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

Anywhere needed within your Razor component, or Blazor client code — either `@inject` or `[Inject]` the `IGeolocation` type. The interface takes the following shape:

```csharp
#nullable enable
namespace Microsoft.JSInterop;

/// <summary>
/// Source generated interface definition of the <c>Geolocation</c> type.
/// </summary>
public interface IGeolocation
{
	/// <summary>
	/// Source generated implementation of <c>window.navigator.geolocation.clearWatch</c>.
	/// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/clearWatch"></a>
	/// </summary>
	ValueTask ClearWatchAsync(double watchId);

	/// <summary>
	/// Source generated implementation of <c>window.navigator.geolocation.getCurrentPosition</c>.
	/// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/getCurrentPosition"></a>
	/// </summary>
	/// <param name="component">The calling Razor (or Blazor) component.</param>
	/// <param name="onSuccessCallbackMethodName">Expects the name of a 
    /// <c>"JSInvokableAttribute"</c> C# method with the following 
    /// <c>System.Action{GeolocationPosition}"</c>.</param>
	/// <param name="onErrorCallbackMethodName">Expects the name of a 
    /// <c>"JSInvokableAttribute"</c> C# method with the following 
    /// <c>System.Action{GeolocationPositionError}"</c>.</param>
	/// <param name="options">The <c>PositionOptions</c> value.</param>
	ValueTask GetCurrentPositionAsync<TComponent>(
        TComponent component, 
        string onSuccessCallbackMethodName, 
        string? onErrorCallbackMethodName = null, 
        PositionOptions? options = null) 
        where TComponent : class;

	/// <summary>
	/// Source generated implementation of <c>window.navigator.geolocation.watchPosition</c>.
	/// <a href="https://developer.mozilla.org/docs/Web/API/Geolocation/watchPosition"></a>
	/// </summary>
	/// <param name="component">The calling Razor (or Blazor) component.</param>
	/// <param name="onSuccessCallbackMethodName">Expects the name of a 
    /// <c>"JSInvokableAttribute"</c> C# method with the following 
    /// <c>System.Action{GeolocationPosition}"</c>.</param>
	/// <param name="onErrorCallbackMethodName">Expects the name of a 
    /// <c>"JSInvokableAttribute"</c> C# method with the following 
    /// <c>System.Action{GeolocationPositionError}"</c>.</param>
	/// <param name="options">The <c>PositionOptions</c> value.</param>
	ValueTask<double> WatchPositionAsync<TComponent>(
        TComponent component, 
        string onSuccessCallbackMethodName, 
        string? onErrorCallbackMethodName = null, 
        PositionOptions? options = null) 
        where TComponent : class;
}
```
