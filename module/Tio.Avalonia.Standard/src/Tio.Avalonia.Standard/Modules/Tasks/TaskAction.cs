using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Tio.Avalonia.Standard.Modules.Tasks;

/// <summary>
/// 可直接绑定到 UI 按钮的任务操作。
/// </summary>
public sealed class TaskAction : ObservableObject
{
    private readonly ManagedTask _task;
    private readonly TaskActionDefinition _definition;

    internal TaskAction(ManagedTask task, TaskActionDefinition definition)
    {
        _task = task;
        _definition = definition;
        Command = new AsyncRelayCommand(ExecuteAsync, CanExecute);
    }

    public string Name => _definition.Name;

    public string? Description => _definition.Description;

    public string? IconKey => _definition.IconKey;

    public IAsyncRelayCommand Command { get; }

    public bool IsVisible => _definition.IsVisible?.Invoke(_task) ?? true;

    public bool IsEnabled => CanExecute();

    internal void Refresh()
    {
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(IsEnabled));
        Command.NotifyCanExecuteChanged();
    }

    private bool CanExecute() => _definition.CanExecute?.Invoke(_task) ?? true;

    private async Task ExecuteAsync()
    {
        try
        {
            await _definition.ExecuteAsync(_task, _task.CancellationToken);
        }
        catch (OperationCanceledException) when (_task.CancellationToken.IsCancellationRequested)
        {
            _task.LogInformation($"操作“{Name}”已取消。");
        }
        catch (Exception exception)
        {
            _task.LogError($"操作“{Name}”执行失败。", exception);
        }
    }
}
