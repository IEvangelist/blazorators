// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Blazor.ExampleConsumer.Models;

public record class TodoItem(
    string Task,
    bool IsCompleted)
{
    internal const string IdPrefix = "todo";

    [JsonIgnore]
    public string Id => $"{IdPrefix}{Regex.Replace(Task, "[^a-zA-Z0-9]", "")}";
}
