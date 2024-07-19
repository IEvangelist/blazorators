// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.JSInterop;

public class ClipboardItem
{
    public IReadOnlyList<string> Types { get; set; } = [];

    public PresentationStyle PresentationStyle { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PresentationStyle
{
    Unspecified,
    Inline,
    Attachment
}