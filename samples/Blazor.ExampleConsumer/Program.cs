// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.ExampleConsumer.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Use source-generated DI bits...
builder.Services.AddLocalStorageServices();
builder.Services.AddSessionStorageServices();
builder.Services.AddGeolocationServices();
builder.Services.AddSpeechSynthesisServices();
builder.Services.AddClipboardServices();

// Custom library bits...
builder.Services.AddSpeechRecognitionServices();

await builder.Build().RunAsync();