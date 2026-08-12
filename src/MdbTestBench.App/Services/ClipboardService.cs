using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace MdbTestBench.App.Services;

public sealed class ClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow is not Window window ||
            TopLevel.GetTopLevel(window)?.Clipboard is not { } clipboard)
            return;
        await clipboard.SetTextAsync(text);
    }
}
