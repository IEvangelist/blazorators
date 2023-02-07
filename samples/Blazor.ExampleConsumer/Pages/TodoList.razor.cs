// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Blazor.ExampleConsumer.Models;
using Microsoft.AspNetCore.Components.Web;

namespace Blazor.ExampleConsumer.Pages;

public sealed partial class TodoList
{
    readonly Dictionary<string, string> _localStorageItems = new();
    HashSet<TodoItem> _todos = new();
    string? _todoValue;

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = null!;

    protected override void OnInitialized() => UpdateTodoItems();

    void UpdateTodoItems()
    {
        var todos = GetLocalStorageKeys()
            .Where(key => key.StartsWith(TodoItem.IdPrefix))
            .Select(key => LocalStorage.GetItem<TodoItem>(key))
            .Where(todo => todo is not null)
            .ToHashSet() ?? new();

        _todos = todos!;

        foreach (var key in GetLocalStorageKeys())
        {
            if (TryGet(key, out TodoItem? todo))
            {
                _localStorageItems[key] = todo.ToString();
                continue;
            }
            if (TryGet(key, out string? @string))
            {
                _localStorageItems[key] = @string;
                continue;
            }
            if (TryGet(key, out decimal num))
            {
                _localStorageItems[key] = num.ToString();
                continue;
            }
            if (TryGet(key, out bool @bool))
            {
                _localStorageItems[key] = @bool.ToString();
                continue;
            }
        }

        bool TryGet<T>(string key, [NotNullWhen(true)] out T? value)
        {
            try
            {
                value = LocalStorage.GetItem<T>(key);
                return value is not null;
            }
            catch
            {
                value = default;
                return false;
            }
        }
    }

    IEnumerable<string> GetLocalStorageKeys()
    {
        var length = LocalStorage.Length;
        for (var i = 0; i < length; ++i)
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
            UpdateTodoItems();
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
        LocalStorage.RemoveItem(todo.Id);
        _todos.RemoveWhere(t => t.Id == todo.Id);
        _localStorageItems.Remove(todo.Id);
    }

    void ClearAll()
    {
        LocalStorage.Clear();
        _todos.Clear();
        _localStorageItems.Clear();
    }

    async Task OnItemChanged(TodoItem _)
    {
        await Task.CompletedTask;

        UpdateTodoItems();
    }
}
