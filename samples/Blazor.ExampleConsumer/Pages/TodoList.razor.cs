// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.ExampleConsumer.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.ExampleConsumer.Pages;

public sealed partial class TodoList
{
    List<TodoItem> _todos = new();
    string? _todoValue;

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = null!;

    protected override void OnInitialized()
    {
        var todos = GetTaskItemKeys()
            .Where(key => key.StartsWith(TodoItem.IdPrefix))
            .Select(key => LocalStorage.GetItem<TodoItem>(key))
            .Where(todo => todo is not null)
            .ToList() ?? new();

        _todos = todos!;
    }
        

    IEnumerable<string> GetTaskItemKeys()
    {
        var length = LocalStorage.Length;
        for (var i = 0; i < length; ++ i)
        {
            if (LocalStorage.Key(i) is { Length: > 0 } key)
            {
                yield return key;
            }
        }
    }

    void AddNewTodo()
    {
        if (_todoValue is not null)
        {
            var todo = new TodoItem(_todoValue, false);
            LocalStorage.SetItem(todo.Id, todo);
            _todos.Add(todo);
            _todoValue = null;
        }
    }

    void OnKeyUp(KeyboardEventArgs args)
    {
        if (args is { Key: "Enter" })
        {
            AddNewTodo();
        }
    }

    void Delete(TodoItem todo)
    {
        if (_todos.Remove(todo))
        {
            LocalStorage.RemoveItem(todo.Id);
        }
    }
}
