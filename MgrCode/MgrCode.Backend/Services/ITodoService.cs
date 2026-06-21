using MgrCode.Backend.Models;

namespace MgrCode.Backend.Services;

public interface ITodoService
{
    Task<List<TodoItem>> GetAllAsync();
    Task AddAsync(string title);
    Task ToggleAsync(int id);
    Task DeleteAsync(int id);
}
