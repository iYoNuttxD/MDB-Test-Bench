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
        if (_viewModel is not null) _viewModel.Logs.Visible.CollectionChanged -= OnLogsChanged;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null) _viewModel.Logs.Visible.CollectionChanged += OnLogsChanged;
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.Logs.AutoScroll || _viewModel.Logs.Visible.Count == 0) return;
        var last = _viewModel.Logs.Visible[^1];
        Dispatcher.UIThread.Post(() => LogsList.ScrollIntoView(last));
    }
}
