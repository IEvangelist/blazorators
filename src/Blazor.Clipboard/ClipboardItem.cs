// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.JSInterop;

public class ClipboardItem
{
    public IReadOnlyList<string> Types { get; private set; } = [];

    public async Task<byte[]> GetTypeAsync(string type)
    {
        // Implement your logic here to fetch Blob data based on type
        // This is just a placeholder
        return await Task.FromResult(Array.Empty<byte>());
    }
}