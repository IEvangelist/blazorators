// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal interface ITypeReference : IObjectType
{
    GenericType Target { get; set; }
    TsType[] TypeArguments { get; set; }
}