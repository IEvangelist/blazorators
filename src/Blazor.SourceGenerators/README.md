# Blazorators: The C# Source Generator for Blazor

This project is capable of parsing TypeScript [type declarations][types] and emitting equivalent C# code 
that implements fully functioning [JavaScript Web APIs][web-apis]. This is the core package for #blazorators. This 
project relies on the [Roslyn source generator APIs][source-gen] to add source files to the compilation context.

[types]: https://www.typescriptlang.org/docs/handbook/2/type-declarations.html
[web-apis]: https://developer.mozilla.org/docs/Web/API
[source-gen]: https://docs.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview?wt.mc_id=dapine

This C# source generator that creates extensions methods on the Blazor WebAssembly JavaScript implementation of the `IJSInProcessRuntime` type. This library provides several NuGet packages:

| NuGet package | NuGet version | Build | Description |
|--|--|--|--|
| [`Blazor.SourceGenerators`](https://www.nuget.org/packages/Blazor.SourceGenerators) | [![NuGet](https://img.shields.io/nuget/v/Blazor.SourceGenerators.svg?style=flat)](https://www.nuget.org/packages/Blazor.SourceGenerators) | [![build](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml) | Core source generator library. |

This package is consumed by the following NuGet packages:

- 📦 [Blazor.LocalStorage.WebAssembly](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly)
- 📦 [Blazor.LocalStorage.Server](https://www.nuget.org/packages/Blazor.LocalStorage.Server)
- 📦 [Blazor.Geolocation.WebAssembly](https://www.nuget.org/packages/Blazor.Geolocation.WebAssembly)
- 📦 [Blazor.Geolocation.Server](https://www.nuget.org/packages/Blazor.Geolocation.Server)

![Blazorators Logo](https://raw.githubusercontent.com/IEvangelist/blazorators/main/logo.png)
