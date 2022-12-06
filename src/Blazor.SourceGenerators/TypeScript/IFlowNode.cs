// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.TypeScript;

public interface IFlowNode
{
    public FlowFlags Flags { get; set; }
    public int Id { get; set; }
}