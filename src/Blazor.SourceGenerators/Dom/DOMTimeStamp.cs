// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.SourceGenerators.Dom;

public record struct DOMTimeStamp
{
    public DateTimeOffset Value { get; set; }

    public static implicit operator DOMTimeStamp(long timestamp)
    {
        DOMTimeStamp timeStamp = new()
        {
            Value = DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
        };

        return timeStamp;
    }
}
