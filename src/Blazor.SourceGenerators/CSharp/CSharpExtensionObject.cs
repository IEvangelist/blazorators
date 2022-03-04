// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

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
        string? namespaceString)
    {
        StringBuilder builder = new();

        AppendUsingDeclarations(builder, options);
        AppendNamespace(builder, namespaceString ?? "Microsoft.JSInterop");
        AppendClassName(existingClassName, builder);

        Indentation indentation = new(0);
        AppendOpeningCurlyBrace(builder, indentation);
        indentation = indentation.Increase();

        foreach (var (index, method) in (Methods ?? new List<CSharpMethod>()).Select())
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
                    ? "<TArg>"
                    : "";

            var (returnType, bareType) =
                GetMethodTypes(isGenericReturnType, isPrimitiveType, isVoid, method, options);

            if (method.IsPureJavaScriptInvocation)
            {
                // Write the method signature:
                // - access modifiers
                // - return type
                // - name
                AppendTripleSlashComments(builder, method, options, indentation);

                builder.Append($"{indentation}public static {returnType} {csharpMethodName}{suffix}{genericTypeArgs}(\r\n");
                if (index.IsFirst)
                {
                    indentation = indentation.Increase();
                }

                // Write method parameters
                builder.Append($"{indentation}this {extendingType} javaScript");
                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.Append(",\r\n");
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        if (index.IsFirst)
                        {
                            indentation = indentation.Increase();
                        }

                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (pi.IsLast)
                        {
                            if (isGenericReturnType || containsAnyGenericParameters)
                            {
                                builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)},\r\n");
                                builder.Append($"{indentation}JsonSerializerOptions? options = null) =>\r\n");
                            }
                            else
                            {
                                builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)}) =>\r\n");
                            }
                        }
                        else
                        {
                            builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)},\r\n");
                        }
                    }

                    if (isVoid)
                    {
                        builder.Append($"{indentation}javaScript.InvokeVoid{suffix}(\r\n");
                        if (index.IsFirst)
                        {
                            indentation = indentation.Increase();
                        }
                        builder.Append($"{indentation}\"{javaScriptMethodName}\",\r\n");
                    }
                    else
                    {
                        builder.Append($"{indentation}javaScript.Invoke{suffix}<{bareType}>(\r\n");
                        if (index.IsFirst)
                        {
                            indentation = indentation.Increase();
                        }
                        builder.Append($"{indentation}\"{javaScriptMethodName}\",\r\n");
                    }

                    // Write method body / expression, and arguments to javaScript.Invoke*
                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        if (ai.IsFirst)
                        {
                            indentation = indentation.Increase();
                        }

                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (ai.IsLast)
                        {
                            if (isGenericReturnType)
                            {
                                builder.Append($"            {parameter.ToArgumentString(isGenericType)})\r\n");
                                builder.Append($"            .FromJson{genericTypeArgs}(options);\r\n\r\n");
                            }
                            else
                            {
                                builder.Append($"            {parameter.ToArgumentString(isGenericType)});\r\n\r\n");
                            }
                        }
                        else
                        {
                            builder.Append($"            {parameter.ToArgumentString(isGenericType)},\r\n");
                        }
                    }

                    indentation = indentation.Decrease();
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
            else if (options.OnlyGeneratePureJS is false) // TODO: non-pure JS is not currently supported...
            {
                // Write the methd signature:
                // - access modifiers
                // - return type
                // - name
                AppendTripleSlashComments(builder, method, options, indentation);
                builder.Append($"    public static {returnType} {csharpMethodName}{suffix}<T>(\r\n");

                // Write method parameters
                builder.Append($"        this {extendingType} javaScript,\r\n");
                builder.Append($"        T dotNetObj");
                if (method.ParameterDefinitions.Count > 0)
                {
                    builder.Append(",\r\n");
                    foreach (var (pi, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (pi.IsLast)
                        {
                            if (isGenericType)
                            {
                                builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)},");
                                builder.Append($"{indentation}JsonSerializerOptions? options = null) where T : class =>\r\n");
                            }
                            else
                            {
                                builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)}) where T : class =>\r\n");
                            }
                        }
                        else
                        {
                            builder.Append($"{indentation}{parameter.ToParameterString(isGenericType)},\r\n");
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
                    foreach (var (ai, parameter) in method.ParameterDefinitions.Select())
                    {
                        var isGenericType = IsGenericParameter(method.RawName, parameter, options);
                        if (ai.IsLast)
                        {
                            if (isGenericReturnType)
                            {
                                builder.Append($"            {parameter.ToArgumentString(isGenericType)})\r\n");
                                builder.Append($"            .FromJson{genericTypeArgs}(options);\r\n\r\n");
                            }
                            else
                            {
                                builder.Append($"            {parameter.ToArgumentString(isGenericType)});\r\n\r\n");
                            }                            
                        }
                        else
                        {
                            builder.Append($"            {parameter.ToArgumentString(isGenericType)},\r\n");
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

        AppendClosingCurlyBrace(builder, indentation);

        var staticPartialClassDefinition = builder.ToString();
        return staticPartialClassDefinition;
    }

    static void AppendUsingDeclarations(StringBuilder builder, GeneratorOptions options)
    {
        if (!options.IsWebAssembly)
        {
            builder.Append("using System.Threading.Tasks;\r\n\r\n");
        }

        if (options.SupportsGenerics)
        {
            builder.Append("using Blazor.Serialization.Extensions;\r\n");
            builder.Append("using System.Text.Json;\r\n\r\n");
        }
    }

    static void AppendNamespace(StringBuilder builder, string namespaceString)
    {
        builder.Append("#nullable enable\r\n");
        builder.Append($"namespace {namespaceString};\r\n\r\n");
    }

    static void AppendClassName(string existingClassName, StringBuilder builder)
    {
        var typeName = existingClassName;
        builder.Append($"public static partial class {typeName}\r\n");
    }

    static void AppendOpeningCurlyBrace(StringBuilder builder, Indentation indentation)
    {
        builder.Append($"{indentation}{{\r\n");
    }

    static void AppendClosingCurlyBrace(StringBuilder builder, Indentation indentation)
    {
        builder.Append($"{indentation}}}\r\n");
    }

    static bool IsGenericReturnType(CSharpMethod method, GeneratorOptions options) =>
        options.GenericMethodDescriptors
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

    static bool IsGenericParameter(string methodName, CSharpType parameter, GeneratorOptions options) =>
        options.GenericMethodDescriptors
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

    static void AppendTripleSlashComments(
        StringBuilder builder, CSharpMethod method, GeneratorOptions options, Indentation indentation)
    {
        var indent = indentation.ToString();
        builder.Append($"{indent}/// <summary>\r\n");

        var jsMethodName = method.RawName.LowerCaseFirstLetter();
        var func = $"{options.PathFromWindow}.{jsMethodName}";

        builder.Append($"{indent}/// Source generated extension method implementation of <c>{func}</c>.\r\n");
        var rootUrl = "https://developer.mozilla.org/docs/Web/API";
        var fullUrl = $"{rootUrl}/{options.TypeName}/{method.RawName.LowerCaseFirstLetter()}";
        builder.Append($"{indent}/// <a href=\"{fullUrl}\"></a>\r\n");
        builder.Append($"{indent}/// </summary>\r\n");
    }

    static (string ReturnType, string BareType) GetMethodTypes(
        bool isGenericReturnType, bool isPrimitiveType, bool isVoid, CSharpMethod method, GeneratorOptions options)
    {
        var primitiveType = isPrimitiveType
            ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
            : method.RawReturnTypeName;

        if (!isVoid && isGenericReturnType)
        {
            var nullable =
                method.IsReturnTypeNullable ? "?" : "";

            return options.IsWebAssembly
                ? ($"TResult{nullable}", primitiveType)
                : ($"ValueTask<TResult{nullable}>", primitiveType);
        }

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

    private readonly record struct Indentation(int Level)
    {
        private readonly int _spaces = 4;

        internal Indentation Increase() => this with { Level = Level + 1 };
        internal Indentation Decrease() => this with { Level = Level - 1 };

        public override string ToString() =>
            new(' ', _spaces * Level);
    }
}
