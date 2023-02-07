// Copyright (c) David Pine. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using Blazor.Serialization.Extensions;
using BlazorServer.ExampleConsumer.Models;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorServer.ExampleConsumer.Pages;

public sealed partial class TodoList
{
    readonly Dictionary<string, string> _localStorageItems = new();
    HashSet<TodoItem> _todos = new();
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
        HashSet<TodoItem> todos = new();
        await foreach (var key in GetLocalStorageKeysAsync())
        {
            if (key.StartsWith(TodoItem.IdPrefix))
            {
                var rawValue = await LocalStorage.GetItemAsync(key);
                if (rawValue.FromJson<TodoItem>() is TodoItem todo)
                {
                    todos.Add(todo);
                    _localStorageItems[key] = todo.ToString();
                    continue;
                }
                if (rawValue is not null)
                {
                    _localStorageItems[key] = rawValue;
                    continue;
                }
                if (bool.TryParse(rawValue, out var @bool))
                {
                    _localStorageItems[key] = @bool.ToString();
                    continue;
                }
                if (decimal.TryParse(rawValue, out var num))
                {
                    _localStorageItems[key] = num.ToString();
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
