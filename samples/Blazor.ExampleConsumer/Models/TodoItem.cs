// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Blazor.ExampleConsumer.Models;

public partial record class TodoItem(
    string Task,
    bool IsCompleted)
{
    internal const string IdPrefix = "todo";

    [JsonIgnore]
    public string Id =>
        Task is null
        ? "<Id>"
        : $"{IdPrefix}{AlphabetOrDigitRegex().Replace(Task, "")}";

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex AlphabetOrDigitRegex();
}
