// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddSingleton<LayoutService>();

// Use source-generated DI bits...
builder.Services.AddLocalStorageServices();
builder.Services.AddSessionStorageServices();
builder.Services.AddGeolocationServices();
builder.Services.AddSpeechSynthesisServices();

// Custom library bits...
builder.Services.AddSpeechRecognitionServices();

await builder.Build().RunAsync();
