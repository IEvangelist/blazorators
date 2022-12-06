// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

public class ExpandResult
{
    public string[] FileNames { get; set; } = Array.Empty<string>();
    public MapLike<WatchDirectoryFlags> WildcardDirectories { get; set; } = MapLike<WatchDirectoryFlags>.Empty;
}