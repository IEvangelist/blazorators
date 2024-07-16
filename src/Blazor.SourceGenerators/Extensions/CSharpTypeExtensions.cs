// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.SourceGenerators.Options;

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpTypeExtensions
{
    internal static bool IsGenericParameter(this CSharpType parameter, string methodName, GeneratorOptions options) =>
        Array.Exists(options.GenericMethodDescriptors ?? [], descriptor =>
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
        });
}
