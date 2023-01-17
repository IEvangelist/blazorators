// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

#nullable disable
namespace Blazor.SourceGenerators.TypeScript.Types;

public sealed class PreFinallyFlow : FlowNode
{
    public FlowNode Antecedent { get; set; }
    public FlowLock Lock { get; set; }
}