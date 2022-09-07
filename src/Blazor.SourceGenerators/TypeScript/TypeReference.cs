// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class TypeReference : ObjectType, ITypeReference
{
    internal GenericType Target { get; set; }
    internal TsType[] TypeArguments { get; set; }
}