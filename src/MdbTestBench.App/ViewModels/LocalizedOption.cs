namespace MdbTestBench.App.ViewModels;

public sealed class LocalizedOption<T>(T value, string displayName) : ViewModelBase
{
    private string _displayName = displayName;
    public T Value { get; } = value;
    public string DisplayName { get => _displayName; private set => SetProperty(ref _displayName, value); }
    public void Relocalize(string displayName) => DisplayName = displayName;
    public override string ToString() => DisplayName;
}
