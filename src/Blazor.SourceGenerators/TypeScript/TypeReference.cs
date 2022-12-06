// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class TypeReference : ObjectType, ITypeReference
{
    public GenericType? Target { get; set; }
    public TsType[]? TypeArguments { get; set; }
}