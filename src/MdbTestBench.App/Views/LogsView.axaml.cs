using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using MdbTestBench.App.ViewModels;

namespace MdbTestBench.App.Views;

public sealed partial class LogsView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public LogsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null) _viewModel.VisibleLogs.CollectionChanged -= OnLogsChanged;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null) _viewModel.VisibleLogs.CollectionChanged += OnLogsChanged;
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.AutoScroll || _viewModel.VisibleLogs.Count == 0) return;
        var last = _viewModel.VisibleLogs[^1];
        Dispatcher.UIThread.Post(() => LogsList.ScrollIntoView(last));
    }
}
