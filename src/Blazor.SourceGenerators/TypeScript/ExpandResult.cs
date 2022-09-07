// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

internal class ExpandResult
{
    internal string[] FileNames { get; set; }
    internal MapLike<WatchDirectoryFlags> WildcardDirectories { get; set; }
}