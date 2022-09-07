// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

internal interface IFlowNode
{
    internal FlowFlags Flags { get; set; }
    internal int Id { get; set; }
}