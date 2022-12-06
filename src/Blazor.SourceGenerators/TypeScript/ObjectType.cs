// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ObjectType : TsType, IObjectType
{
    ObjectFlags IObjectType.ObjectFlags? { get; set; }
}