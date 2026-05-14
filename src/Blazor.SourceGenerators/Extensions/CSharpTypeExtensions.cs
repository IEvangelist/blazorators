// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Extensions;

internal static class CSharpTypeExtensions
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="parameter"/> corresponds to a generic
    /// parameter described by one of the
    /// <see cref="GeneratorOptions.GenericMethodDescriptors"/>.
    /// </summary>
    /// <remarks>
    /// A descriptor of <c>"methodName:parameterName"</c> matches when both segments
    /// equal <paramref name="methodName"/> and <see cref="CSharpType.RawName"/>
    /// respectively. Descriptors without a colon describe a generic return type and
    /// always return <c>false</c> from this method (see
    /// <see cref="CSharpMethodExtensions.IsGenericReturnType"/>).
    /// </remarks>
    internal static bool IsGenericParameter(this CSharpType parameter, string methodName, GeneratorOptions options) =>
        options.GenericMethodDescriptors
            ?.Any(descriptor =>
            {
                var colon = descriptor.IndexOf(':');
                if (colon < 0)
                {
                    return false;
                }

                var descriptorMethod = descriptor.Substring(0, colon);
                var descriptorParameter = descriptor.Substring(colon + 1);

                return descriptorMethod == methodName
                    && descriptorParameter == parameter.RawName;
            })
            ?? false;
}
