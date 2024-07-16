// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class GeneratorExecutionContextExtensions
{
    internal static GeneratorExecutionContext AddDependentTypesSource(this GeneratorExecutionContext context, CSharpTopLevelObject topLevelObject)
    {
        var dependentTypes = topLevelObject.AllDependentTypes.Where(type => !type.Object.IsActionParameter);
        foreach (var (type, dependentObj) in dependentTypes)
        {
            context.AddSource(type.ToGeneratedFileName(),
                SourceText.From(dependentObj.ToString(),
                Encoding.UTF8));
        }

        return context;
    }

    internal static GeneratorExecutionContext AddInterfaceSource(
        this GeneratorExecutionContext context,
        CSharpTopLevelObject topLevelObject,
        string @interface,
        GeneratorOptions options,
        string? @namespace)
    {
        context.AddSource(
            $"{@interface}".ToGeneratedFileName(),
            SourceText.From(
                topLevelObject.ToInterfaceString(options, @namespace),
                Encoding.UTF8));

        return context;
    }

    internal static GeneratorExecutionContext AddImplementationSource(
        this GeneratorExecutionContext context,
        CSharpTopLevelObject topLevelObject,
        string implementation,
        GeneratorOptions options,
        string? @namespace)
    {
        context.AddSource(
            $"{implementation}".ToGeneratedFileName(),
            SourceText.From(
                topLevelObject.ToImplementationString(options, @namespace),
                Encoding.UTF8));

        return context;
    }

    internal static GeneratorExecutionContext AddDependencyInjectionExtensionsSource(
        this GeneratorExecutionContext context,
        CSharpTopLevelObject _,
        string implementation,
        GeneratorOptions options)
    {
        context.AddSource(
            $"{options.Implementation.ToImplementationName(false)}ServiceCollectionExtensions".ToGeneratedFileName(),
            SourceText.From(
                CSharpTopLevelObject.ToServiceCollectionExtensions(options, implementation),
                Encoding.UTF8));

        return context;
    }
}
