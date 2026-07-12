using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using System;

namespace SBtools.ViewModels;

public class LogTabViewModel : ViewModelBase
{
    public ObservableCollection<string> LogEntries { get; } = new();

    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public LogTabViewModel()
    {
        ClearCommand = ReactiveCommand.Create(() => LogEntries.Clear());
        SaveCommand = ReactiveCommand.Create(SaveLog);
        AddLog("日志系统已初始化");
    }

    private void SaveLog()
    {
        // 简单实现：弹窗提示（实际可保存到文件）
        AddLog("保存日志功能待实现");
    }

    public void AddLog(string message)
    {
        LogEntries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss,fff} - {message}");
    }
}