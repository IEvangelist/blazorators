// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Blazor.SourceGenerators.Extensions;
using Blazor.SourceGenerators.Types;

namespace Blazor.SourceGenerators.CSharp
{
    public record CSharpExtensionObject(string RawTypeName)
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
            StringBuilder builder = new("using System.Threading.Tasks;\r\n\r\n");

            builder.Append("#nullable enable\r\n");
            builder.Append($"namespace {namespaceString}\r\n");
            builder.Append("{\r\n");

            var typeName = existingClassName;
            builder.Append($"    public static partial class {typeName}\r\n");
            builder.Append("    {\r\n");

            foreach (var method in Methods ?? Enumerable.Empty<CSharpMethod>())
            {
                var isVoid = method.RawReturnTypeName == "void";
                var isPrimitiveType = TypeMap.PrimitiveTypes.IsPrimitiveType(method.RawReturnTypeName);

                var javaScriptMethodName = options.PathFromWindow is not null
                    ? $"{options.PathFromWindow}.{method.RawName}"
                    : method.RawName;
                var csharpMethodName = method.RawName.CapitalizeFirstLetter();

                if (method.IsPureJavaScriptInvocation)
                {
                    var returnType = isPrimitiveType
                        ? $"ValueTask<{TypeMap.PrimitiveTypes[method.RawReturnTypeName]}>"
                        : isVoid
                            ? "ValueTask"
                            : method.RawReturnTypeName;

                    var bareType = isPrimitiveType
                        ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
                        : method.RawReturnTypeName;

                    // Write the methd signature:
                    // - access modifiers
                    // - return type
                    // - name
                    builder.Append($"        public static {returnType} {csharpMethodName}Async(\r\n");

                    // Write method parameters
                    builder.Append($"            this IJSRuntime javaScript");
                    if (method.ParameterDefinitions.Count > 0)
                    {
                        builder.Append(",\r\n");
                        foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                        {
                            if (index == method.ParameterDefinitions.Count - 1)
                            {
                                builder.Append($"            {parameter.ToParameterString()}) =>\r\n");
                            }
                            else
                            {
                                builder.Append($"            {parameter.ToParameterString()},\r\n");
                            }
                        }

                        if (isVoid)
                        {
                            builder.Append($"            javaScript.InvokeVoidAsync(\r\n");
                            builder.Append($"                \"{javaScriptMethodName}\",\r\n");
                        }
                        else
                        {
                            builder.Append($"            javaScript.InvokeAsync<{bareType}>(\r\n");
                            builder.Append($"                \"{javaScriptMethodName}\",\r\n");
                        }

                        // Write method body / expression
                        foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                        {
                            if (index == method.ParameterDefinitions.Count - 1)
                            {
                                builder.Append($"                {parameter.ToArgumentString()});\r\n\r\n");
                            }
                            else
                            {
                                builder.Append($"                {parameter.ToArgumentString()},\r\n");
                            }
                        }
                    }
                    else
                    {
                        builder.Append(") =>\r\n");
                        if (isVoid)
                        {
                            builder.Append($"            javaScript.InvokeVoidAsync(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                        else
                        {
                            builder.Append($"            javaScript.InvokeAsync<{bareType}>(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                    }
                }
                else if (options.OnlyGeneratePureJS is false)
                {
                    var returnType = isPrimitiveType
                       ? $"ValueTask<{TypeMap.PrimitiveTypes[method.RawReturnTypeName]}>"
                       : isVoid
                           ? "ValueTask"
                           : method.RawReturnTypeName;

                    var bareType = isPrimitiveType
                        ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
                        : method.RawReturnTypeName;

                    // Write the methd signature:
                    // - access modifiers
                    // - return type
                    // - name
                    builder.Append($"        public static {returnType} {csharpMethodName}Async<T>(\r\n");

                    // Write method parameters
                    builder.Append($"            this IJSRuntime javaScript,\r\n");
                    builder.Append($"            T dotNetObj");
                    if (method.ParameterDefinitions.Count > 0)
                    {
                        builder.Append(",\r\n");
                        foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                        {
                            if (index == method.ParameterDefinitions.Count - 1)
                            {
                                builder.Append($"            {parameter.ToParameterString()}) where T : class =>\r\n");
                            }
                            else
                            {
                                builder.Append($"            {parameter.ToParameterString()},\r\n");
                            }
                        }

                        if (isVoid)
                        {
                            builder.Append($"            javaScript.InvokeVoidAsync(\r\n");
                            builder.Append($"                \"{javaScriptMethodName}\",\r\n");
                        }
                        else
                        {
                            builder.Append($"            javaScript.InvokeAsync<{bareType}>(\r\n");
                            builder.Append($"                \"{javaScriptMethodName}\",\r\n");
                        }

                        builder.Append($"                DotNetObjectReference.Create(dotNetObj),\r\n");

                        // Write method body / expression
                        foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                        {
                            if (index == method.ParameterDefinitions.Count - 1)
                            {
                                builder.Append($"                {parameter.ToArgumentString()});\r\n\r\n");
                            }
                            else
                            {
                                builder.Append($"                {parameter.ToArgumentString()},\r\n");
                            }
                        }
                    }
                    else
                    {
                        builder.Append(") =>\r\n");
                        if (isVoid)
                        {
                            builder.Append($"            javaScript.InvokeVoidAsync(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                        else
                        {
                            builder.Append($"            javaScript.InvokeAsync<{bareType}>(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                    }
                }
            }

            builder.Append("    }\r\n");
            builder.Append("}\r\n");

            var staticPartialClassDefinition = builder.ToString();
            return staticPartialClassDefinition;
        }
    }
}