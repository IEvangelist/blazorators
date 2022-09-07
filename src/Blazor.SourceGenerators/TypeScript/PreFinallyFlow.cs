// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class PreFinallyFlow : FlowNode
{
    internal FlowNode Antecedent { get; set; }
    internal FlowLock Lock { get; set; }
}