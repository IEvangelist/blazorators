// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using Blazor.SourceGenerators.Extensions;
using Blazor.SourceGenerators.Types;

namespace Blazor.SourceGenerators.CSharp;

internal sealed record CSharpExtensionObject(string RawTypeName)
{
    private List<CSharpMethod>? _methods = null!;
    private List<CSharpProperty>? _properties = null!;
    private Dictionary<string, CSharpObject>? _dependentTypes = null!;

    public List<CSharpProperty>? Properties
    {
        get => _properties ??= new();
        init => _properties = value;
    }

    public List<CSharpMethod>? Methods
    {
        get => _methods ??= new();
        init => _methods = value;
    }

    public Dictionary<string, CSharpObject>? DependentTypes
    {
        get => _dependentTypes ??= new(StringComparer.OrdinalIgnoreCase);
        init => _dependentTypes = value;
    }

    public int MemberCount => Properties!.Count + Methods!.Count;

    internal string ToStaticPartialClassString(
        GeneratorOptions options,
        string existingClassName,
        string? namespaceString = "Microsoft.JSInterop")
    {
        StringBuilder builder =
            new(options.IsWebAssembly ? "" : "using System.Threading.Tasks;\r\n\r\n");

        builder.Append("#nullable enable\r\n");
        builder.Append($"namespace {namespaceString};\r\n\r\n");

        var typeName = existingClassName;
        builder.Append($"public static partial class {typeName}\r\n");
        builder.Append("{\r\n");

        static bool IsGenericReturnType(CSharpMethod method, GeneratorOptions options)
        {
            return options.GenericMethodDescriptors
                ?.Any(descriptor =>
                {
                    // If the descriptor describes a parameter, it's not a generic return.
                    // TODO: consider APIs that might do this.
                    if (descriptor.Contains(":"))
                    {
                        return false;
                    }

                    // If the descriptor is the method name
                    return descriptor == method.RawName;
                })
                ?? false;
        }

        static bool IsGenericParameter(string methodName, CSharpType parameter, GeneratorOptions options)
        {
            return options.GenericMethodDescriptors
                ?.Any(descriptor =>
                {
                    if (!descriptor.StartsWith(methodName))
                    {
                        return false;
                    }

                    if (descriptor.Contains(":"))
                    {
                        var nameParamPair = descriptor.Split(':');
                        return nameParamPair[1].StartsWith(parameter.RawName);
                    }

                    return false;
                })
                ?? false;
        }

        foreach (var method in Methods ?? Enumerable.Empty<CSharpMethod>())
        {
            var isVoid = method.RawReturnTypeName == "void";
            var isPrimitiveType = TypeMap.PrimitiveTypes.IsPrimitiveType(method.RawReturnTypeName);
            var javaScriptMethodName = options.PathFromWindow is not null
                ? $"{options.PathFromWindow}.{method.RawName}"
                : method.RawName;
            var csharpMethodName = method.RawName.CapitalizeFirstLetter();
            var isGenericReturnType = IsGenericReturnType(method, options);
            var containsAnyGenericParameters =
                method.ParameterDefinitions.Any(p => IsGenericParameter(method.RawName, p, options));
            var (suffix, extendingType) = options.IsWebAssembly ? ("", "IJSInProcessRuntime") : ("Async", "IJSRuntime");
            var genericTypeArgs = isGenericReturnType
                ? "<TResult>"
                : containsAnyGenericParameters
                    ? "<T>"
                    : "";
            if (method.IsPureJavaScriptInvocation)
            {
                var (returnType, bareType) =
                    GetMethodTypes(isGenericReturnType, isPrimitiveType, isVoid, method, options);

                // Write the method signature:
                // - access modifiers
                // - return type
                // - name
                AppendTripleSlashComments(builder, method, options);
                builder.Append($"    public static {returnType} {csharpMethodName}{suffix}{genericTypeArgs}(\r\n");

                // Write method parameters
                builder.Append($"        this {extendingType} javaScript");
                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.Append(",\r\n");
                    foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                    {
                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (index == method.ParameterDefinitions.Count - 1)
                        {
                            builder.Append($"        {parameter.ToParameterString(isGenericType)}) =>\r\n");
                        }
                        else
                        {
                            builder.Append($"        {parameter.ToParameterString(isGenericType)},\r\n");
                        }
                    }

                    if (isVoid)
                    {
                        builder.Append($"        javaScript.InvokeVoid{suffix}(\r\n");
                        builder.Append($"            \"{javaScriptMethodName}\",\r\n");
                    }
                    else
                    {
                        builder.Append($"        javaScript.Invoke{suffix}<{bareType}>(\r\n");
                        builder.Append($"            \"{javaScriptMethodName}\",\r\n");
                    }

                    // Write method body / expression, and arguments to javaScript.Invoke*
                    foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                    {
                        if (index == method.ParameterDefinitions.Count - 1)
                        {
                            builder.Append($"            {parameter.ToArgumentString()});\r\n\r\n");
                        }
                        else
                        {
                            builder.Append($"            {parameter.ToArgumentString()},\r\n");
                        }
                    }
                }
                else
                {
                    builder.Append(") =>\r\n");
                    if (isVoid)
                    {
                        builder.Append($"        javaScript.InvokeVoid{suffix}(\"{javaScriptMethodName}\");\r\n\r\n");
                        continue;
                    }
                    else
                    {
                        builder.Append($"        javaScript.Invoke{suffix}<{bareType}>(\"{javaScriptMethodName}\");\r\n\r\n");
                        continue;
                    }
                }
            }
            else if (options.OnlyGeneratePureJS is false)
            {
                var (returnType, bareType) =
                    GetMethodTypes(isGenericReturnType, isPrimitiveType, isVoid, method, options);

                // Write the methd signature:
                // - access modifiers
                // - return type
                // - name
                AppendTripleSlashComments(builder, method, options);
                builder.Append($"    public static {returnType} {csharpMethodName}{suffix}<T>(\r\n");

                // Write method parameters
                builder.Append($"        this {extendingType} javaScript,\r\n");
                builder.Append($"        T dotNetObj");
                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.Append(",\r\n");
                    foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                    {
                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (index == method.ParameterDefinitions.Count - 1)
                        {
                            builder.Append($"        {parameter.ToParameterString(isGenericType)}) where T : class =>\r\n");
                        }
                        else
                        {
                            builder.Append($"        {parameter.ToParameterString(isGenericType)},\r\n");
                        }
                    }

                    if (isVoid)
                    {
                        builder.Append($"        javaScript.InvokeVoid{suffix}(\r\n");
                        builder.Append($"            \"{javaScriptMethodName}\",\r\n");
                    }
                    else
                    {
                        builder.Append($"        javaScript.Invoke{suffix}<{bareType}>(\r\n");
                        builder.Append($"            \"{javaScriptMethodName}\",\r\n");
                    }

                    builder.Append($"            DotNetObjectReference.Create(dotNetObj),\r\n");

                    // Write method body / expression
                    foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                    {
                        if (index == method.ParameterDefinitions.Count - 1)
                        {
                            builder.Append($"            {parameter.ToArgumentString()});\r\n\r\n");
                        }
                        else
                        {
                            builder.Append($"            {parameter.ToArgumentString()},\r\n");
                        }
                    }
                }
                else
                {
                    builder.Append(") =>\r\n");
                    if (isVoid)
                    {
                        builder.Append($"        javaScript.InvokeVoid{suffix}(\"{javaScriptMethodName}\");\r\n\r\n");
                        continue;
                    }
                    else
                    {
                        builder.Append($"        javaScript.Invoke{suffix}<{bareType}>(\"{javaScriptMethodName}\");\r\n\r\n");
                        continue;
                    }
                }
            }
        }

        builder.Append("}\r\n");

        var staticPartialClassDefinition = builder.ToString();
        return staticPartialClassDefinition;
    }

    static StringBuilder AppendTripleSlashComments(
        StringBuilder builder, CSharpMethod method, GeneratorOptions options)
    {
        builder.Append($"    /// <summary>\r\n");

        var jsMethodName = method.RawName.LowerCaseFirstLetter();
        var func = $"{options.PathFromWindow}.{jsMethodName}";

        builder.Append($"    /// Source generated extension method implementation of <c>{func}</c>.\r\n");
        var rootUrl = "https://developer.mozilla.org/en-US/docs/Web/API";
        var fullUrl = $"{rootUrl}/{options.TypeName}/{method.RawName.LowerCaseFirstLetter()}";
        builder.Append($"    /// <a href=\"{fullUrl}\"></a>\r\n");
        builder.Append($"    /// </summary>\r\n");

        return builder;
    }

    static (string ReturnType, string BareType) GetMethodTypes(
        bool isGenericReturnType, bool isPrimitiveType, bool isVoid, CSharpMethod method, GeneratorOptions options)
    {
        // TODO: Ugh, this is really ugly! Need to work on making this easier to parse.
        if (!isVoid && isGenericReturnType)
        {
            return options.IsWebAssembly
                ? ("TResult", "TResult")
                : ("ValueTask<TResult>", "TResult");
        }


        var primitiveType = isPrimitiveType
            ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
            : method.RawReturnTypeName;

        if (options.IsWebAssembly)
        {
            var returnType = isPrimitiveType
                ? primitiveType
                    : isVoid
                        ? "void"
                        : method.RawReturnTypeName;

            return (returnType, primitiveType);
        }
        else
        {
            var returnType = isPrimitiveType
                ? $"ValueTask<{primitiveType}>"
                    : isVoid
                        ? "ValueTask"
                        : method.RawReturnTypeName;

            return (returnType, primitiveType);
        }
    }
}
