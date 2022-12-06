// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public class AnonymousType : ObjectType
{
    public AnonymousType Target? { get; set; }
    public TypeMapper Mapper? { get; set; }
}