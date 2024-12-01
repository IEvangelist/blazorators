// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using Blazor.Serialization.Extensions;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorServer.ExampleConsumer.Pages;

public sealed partial class TodoList
{
    readonly Dictionary<string, string> _localStorageItems = [];
    HashSet<TodoItem> _todos = [];
    string? _todoValue;

    [Inject]
    public ILocalStorageService LocalStorage { get; set; } = null!;

    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
        {
            return Task.CompletedTask;
        }

        return UpdateTodoItemsAsync();
    }

    async Task UpdateTodoItemsAsync()
    {
        HashSet<TodoItem> todos = [];
        await foreach (var key in GetLocalStorageKeysAsync())
        {
            if (key.StartsWith(TodoItem.IdPrefix))
            {
                var todo = await LocalStorage.GetItemAsync<TodoItem?>(key);
                if (todo is not null)
                {
                    todos.Add(todo);
                    _localStorageItems[key] = todo.ToString();
                    continue;
                }
            }
        }

        _todos = todos!;
    }

    async IAsyncEnumerable<string> GetLocalStorageKeysAsync()
    {
        var length = await LocalStorage.Length;
        for (var i = 0; i < length; ++i)
        {
            if (await LocalStorage.KeyAsync(i) is { Length: > 0 } key)
            {
                yield return key;
            }
        }
    }

    async Task AddNewTodoAsync()
    {
        if (_todoValue is not null)
        {
            var todo = new TodoItem(_todoValue, false);
            await LocalStorage.SetItemAsync(todo.Id, todo.ToJson());
            await UpdateTodoItemsAsync();
            _todoValue = null;
        }
    }

    Task OnKeyUp(KeyboardEventArgs args) =>
        args is { Key: "Enter" }
            ? AddNewTodoAsync()
            : Task.CompletedTask;

    async Task Delete(TodoItem todo)
    {
        await LocalStorage.RemoveItemAsync(todo.Id);
        _todos.RemoveWhere(t => t.Id == todo.Id);
        _localStorageItems.Remove(todo.Id);
    }

    async Task ClearAll()
    {
        await LocalStorage.ClearAsync();
        _todos.Clear();
        _localStorageItems.Clear();
    }

    async Task OnItemChanged(TodoItem _)
    {
        await Task.CompletedTask;
        await UpdateTodoItemsAsync();
    }
}
