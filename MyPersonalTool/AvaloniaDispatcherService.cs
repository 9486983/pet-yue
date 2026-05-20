using Avalonia.Threading;
using MyPersonalTool.Core.Interfaces;

namespace MyPersonalTool.Services;

/// <summary>Avalonia 调度器实现</summary>
public class AvaloniaDispatcherService : IDispatcherService
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
