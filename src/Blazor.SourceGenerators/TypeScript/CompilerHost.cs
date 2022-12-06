// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class CompilerHost : ModuleResolutionHost
{
    public WriteFileCallback WriteFile? { get; set; }
}