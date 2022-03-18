# Blazorators: The Serialization Library

This project is a serialization library used by the `Blazor.SourceGenerators` code library. It enables
the support of generics, and provides that ability for the source generator to use convenience-based 
extension methods to serialize and deserialize generics when performing various Blazor JavaScript interop.

| NuGet package | NuGet version | Build | Description |
|--|--|--|
| [`Blazor.Serialization`](https://www.nuget.org/packages/Blazor.Serialization) | [![NuGet](https://img.shields.io/nuget/v/Blazor.Serialization.svg?style=flat)](https://www.nuget.org/packages/Blazor.Serialization) | [![build](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml/badge.svg)](https://github.com/IEvangelist/blazorators/actions/workflows/build-validation.yml) | Common serialization library, required in some scenarios when using generics. |

It's consumed by the following NuGet packages:

- 📦 [Blazor.LocalStorage.WebAssembly](https://www.nuget.org/packages/Blazor.LocalStorage.WebAssembly)
- 📦 [Blazor.Geolocation.WebAssembly](https://www.nuget.org/packages/Blazor.Geolocation.WebAssembly)
- 📦 [Blazor.Geolocation.Server](https://www.nuget.org/packages/Blazor.Geolocation.Server)

![Blazorators Logo](https://raw.githubusercontent.com/IEvangelist/blazorators/main/logo.png)
