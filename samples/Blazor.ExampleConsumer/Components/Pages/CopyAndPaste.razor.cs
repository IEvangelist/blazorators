// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Components.Pages;

public partial class CopyAndPaste
{
    private string? _content;

    private async Task ReadAsync()
    {
        _content = await Clipboard.ReadTextAsync();
    }

    private async Task WriteAsync()
    {
        await Clipboard.WriteTextAsync(_content ?? "");
    }
}
