// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TypeScript.TypeConverter.Extensions;
using TypeScript.TypeConverter.Types;

namespace TypeScript.TypeConverter.CSharp
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

        public string ToStaticPartialClassString()
        {
            StringBuilder builder = new("namespace Microsoft.JSInterop\r\n");

            builder.Append("{\r\n\r\n");

            var typeName = RawTypeName.EndsWith("Extensions") ? RawTypeName : $"{RawTypeName}Extensions";
            builder.Append($"    public static partial class {typeName}\r\n");
            builder.Append("    {\r\n");

            foreach (var method in Methods ?? Enumerable.Empty<CSharpMethod>())
            {
                var isVoid = method.RawReturnTypeName == "void";
                var isPrimitiveType = TypeMap.PrimitiveTypes.IsPrimitiveType(method.RawReturnTypeName);

                var javaScriptMethodName = method.RawName;
                var csharpMethodName = method.RawName.CapitalizeFirstLetter();

                if (method.IsPureJavaScriptInvocation)
                {
                    var returnType = isPrimitiveType
                        ? $"ValueTask<{TypeMap.PrimitiveTypes[method.RawReturnTypeName]}>"
                        : isVoid
                            ? "ValueTask"
                            : method.RawReturnTypeName;

                    var genericType = isPrimitiveType
                        ? TypeMap.PrimitiveTypes[method.RawReturnTypeName]
                        : method.RawReturnTypeName;

                    builder.Append($"        public static {returnType} {csharpMethodName}Async(\r\n");
                    builder.Append($"            this IJSRuntime javaScript");

                    if (method.ParameterDefinitions.Count == 0)
                    {
                        builder.Append(") =>\r\n");
                        if (isVoid)
                        {
                            builder.Append($"            javaScript.InvokeVoidAsync(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                        else
                        {
                            builder.Append($"            javaScript.InvokeAsync<{genericType}>(\"{javaScriptMethodName}\");\r\n\r\n");
                            continue;
                        }
                    }
                    else
                    {
                        foreach (var (index, parameter) in method.ParameterDefinitions.Select((p, i) => (i, p)))
                        {
                            if (index == 0)
                            {
                                if (isVoid)
                                {
                                    builder.Append($"            javaScript.InvokeVoidAsync(\"{javaScriptMethodName}\",\r\n");
                                }
                                else
                                {
                                    builder.Append($"            javaScript.InvokeAsync<{genericType}>(\"{javaScriptMethodName}\",\r\n");
                                }
                            }

                            if (index == method.ParameterDefinitions.Count - 1)
                            {
                                builder.Append($"                ${parameter.RawName});\r\n");
                            }
                            else
                            {
                                builder.Append($"                ${parameter.RawName},\r\n");
                            }
                        }
                    }
                }
                else
                {
                    builder.Append($"        public static ValueTask {csharpMethodName}Async(\r\n");
                    // TODO: implement
                    builder.Append($"        ) => new ValueTask();\r\n");
                }
            }

            builder.Append("    }\r\n");
            builder.Append("}\r\n");
            return builder.ToString();
        }
    }
}