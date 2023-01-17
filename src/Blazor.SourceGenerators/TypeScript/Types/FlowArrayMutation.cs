// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class FlowArrayMutation : FlowNode
{
    public Node Node { get; set; }
    public FlowNode Antecedent { get; set; }
}