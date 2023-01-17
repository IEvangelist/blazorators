// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public class ObjectType : TypeScriptType, IObjectType
{
    public ObjectFlags ObjectFlags { get; set; }
}