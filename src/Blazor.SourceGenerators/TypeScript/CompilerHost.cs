// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class CompilerHost : ModuleResolutionHost
{
    internal WriteFileCallback WriteFile { get; set; }
}