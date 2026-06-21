using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

public class TodoService : ITodoService
{
    private readonly List<TodoItem> _items = new();
    private int _nextId = 1;

    public Task<List<TodoItem>> GetAllAsync()
    {
        return Task.FromResult(_items.ToList());
    }

    public Task AddAsync(string title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            _items.Add(new TodoItem { Id = _nextId++, Title = title });
        }
        return Task.CompletedTask;
    }

    public Task ToggleAsync(int id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item != null)
            item.IsCompleted = !item.IsCompleted;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id)
    {
        _items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }
}
