// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class AnonymousType : ObjectType
{
    public AnonymousType Target { get; set; }
    public TypeMapper Mapper { get; set; }
}