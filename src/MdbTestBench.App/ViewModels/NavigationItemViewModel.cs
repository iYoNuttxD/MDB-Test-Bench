namespace MdbTestBench.App.ViewModels;

public sealed class NavigationItemViewModel(string id, string title, string description) : ViewModelBase
{
    private string _title = title;
    private string _description = description;
    public string Id { get; } = id;
    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Description { get => _description; private set => SetProperty(ref _description, value); }
    public void Relocalize(string title, string description) { Title = title; Description = description; }
}
