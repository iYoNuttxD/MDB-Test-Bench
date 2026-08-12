using Avalonia.Controls;

namespace MdbTestBench.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closed += OnClosed;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is not ViewModels.MainWindowViewModel viewModel) return;
        await viewModel.SaveWindowStateAsync(Width, Height);
        await viewModel.DisposeAsync();
    }
}
