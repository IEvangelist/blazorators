// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Models;

public record class TodoItem(
    string Task,
    bool IsCompleted)
{
    internal const string IdPrefix = "todo:";

    [JsonIgnore]
    public string Id => $"{IdPrefix}{GetHashCode()}";

    public override int GetHashCode() =>
    ((EqualityComparer<Type>.Default.GetHashCode(typeof(TodoItem)) * -1521134295) +
        EqualityComparer<string>.Default.GetHashCode(Task)) * -1521134295;
}
