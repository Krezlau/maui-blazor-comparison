using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MgrCode.Backend.Models;
using MgrCode.Backend.Services;

namespace MgrCode.Backend.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ITodoService _todoService;

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _newTodoTitle = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<TodoItem> TodoItems { get; } = new();

    public MainViewModel(ITodoService todoService)
    {
        _todoService = todoService;
    }

    partial void OnCountChanged(int value)
    {
        CountText = value == 1 ? $"Clicked {value} time" : $"Clicked {value} times";
    }

    [ObservableProperty]
    private string _countText = "Click the button";

    [RelayCommand]
    private void IncrementCount()
    {
        Count++;
    }

    [RelayCommand]
    private async Task LoadTodosAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var items = await _todoService.GetAllAsync();
            TodoItems.Clear();
            foreach (var item in items)
                TodoItems.Add(item);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTodoAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;

        await _todoService.AddAsync(NewTodoTitle);
        NewTodoTitle = string.Empty;
        await LoadTodosAsync();
    }

    [RelayCommand]
    private async Task ToggleTodoAsync(TodoItem item)
    {
        await _todoService.ToggleAsync(item.Id);
        await LoadTodosAsync();
    }

    [RelayCommand]
    private async Task DeleteTodoAsync(TodoItem item)
    {
        await _todoService.DeleteAsync(item.Id);
        await LoadTodosAsync();
    }
}
