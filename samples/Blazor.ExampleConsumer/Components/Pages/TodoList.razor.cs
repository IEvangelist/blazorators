// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

namespace Blazor.ExampleConsumer.Components.Pages;

public sealed partial class TodoList(ILocalStorageService localStorage)
{
    readonly Dictionary<string, string> _localStorageItems = [];
    HashSet<TodoItem> _todos = [];
    string? _todoValue;

    protected override void OnInitialized() => UpdateTodoItems();

    void UpdateTodoItems()
    {
        var todos = GetLocalStorageKeys()
            .Where(key => key.StartsWith(TodoItem.IdPrefix))
            .Select(key => localStorage.GetItem<TodoItem>(key))
            .Where(todo => todo is not null)
            .ToHashSet() ?? [];

        _todos = todos!;

        foreach (var key in GetLocalStorageKeys())
        {
            if (TryGet(key, out TodoItem? todo) && todo is not null)
            {
                _localStorageItems[key] = todo.ToString();
                continue;
            }
            if (TryGet(key, out string? @string) && @string is not null)
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
                value = localStorage.GetItem<T>(key);
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
        var length = localStorage.Length;
        for (var i = 0; i < length; ++i)
        {
            if (localStorage.Key(i) is { Length: > 0 } key)
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
            localStorage.SetItem(todo.Id, todo);
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
        localStorage.RemoveItem(todo.Id);
        _todos.RemoveWhere(t => t.Id == todo.Id);
        _localStorageItems.Remove(todo.Id);
    }

    void ClearAll()
    {
        localStorage.Clear();
        _todos.Clear();
        _localStorageItems.Clear();
    }

    async Task OnItemChanged(TodoItem _)
    {
        await Task.CompletedTask;

        UpdateTodoItems();
    }
}
