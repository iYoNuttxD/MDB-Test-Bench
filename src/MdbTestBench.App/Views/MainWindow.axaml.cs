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
        try
        {
            await viewModel.SaveWindowStateAsync(Width, Height);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Unable to save window state: {exception.Message}");
        }
        try
        {
            await viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Unable to dispose application services: {exception.Message}");
        }
    }
}
