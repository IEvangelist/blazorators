﻿// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class SourceProductionContextExtensions
{
    internal static SourceProductionContext AddDependentTypesSource(
        this SourceProductionContext context,
        CSharpTopLevelObject topLevelObject)
    {
        foreach (var (type, dependentObj) in
                    topLevelObject.AllDependentTypes.Where(
                        t => !t.Object.IsActionParameter))
        {
            context.AddSource(type.ToGeneratedFileName(),
                SourceText.From(dependentObj.ToString(),
                Encoding.UTF8));
        }

        return context;
    }

    internal static SourceProductionContext AddInterfaceSource(
        this SourceProductionContext context,
        CSharpTopLevelObject topLevelObject,
        string @interface,
        GeneratorOptions options,
        string? @namespace)
    {
        context.AddSource(
            $"{@interface}".ToGeneratedFileName(),
            SourceText.From(
                topLevelObject.ToInterfaceString(
                    options,
                    @namespace),
                Encoding.UTF8));

        return context;
    }

    internal static SourceProductionContext AddImplementationSource(
        this SourceProductionContext context,
        CSharpTopLevelObject topLevelObject,
        string implementation,
        GeneratorOptions options,
        string? @namespace)
    {
        context.AddSource(
            $"{implementation}".ToGeneratedFileName(),
            SourceText.From(
                topLevelObject.ToImplementationString(
                    options,
                    @namespace),
                Encoding.UTF8));

        return context;
    }

    internal static SourceProductionContext AddDependencyInjectionExtensionsSource(
        this SourceProductionContext context,
        CSharpTopLevelObject topLevelObject,
        string implementation,
        GeneratorOptions options)
    {
        context.AddSource(
            $"{options.Implementation.ToImplementationName(false)}ServiceCollectionExtensions".ToGeneratedFileName(),
            SourceText.From(
                topLevelObject.ToServiceCollectionExtensions(
                    options,
                    implementation),
                Encoding.UTF8));

        return context;
    }
}
