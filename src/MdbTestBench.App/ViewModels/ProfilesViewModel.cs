using System.Collections.ObjectModel;
using System.Windows.Input;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;

namespace MdbTestBench.App.ViewModels;

public sealed class ProfilesViewModel : ViewModelBase
{
    public event EventHandler? SelectionChanged;
    private readonly ProfileRepository _repository;
    private MdbProfile? _selected;
    private string _message = "Built-in profiles are read-only.";
    private string _importPath = string.Empty;

    public ProfilesViewModel(ProfileRepository repository, string lastProfileId)
    {
        _repository = repository;
        foreach (var profile in repository.LoadAll()) Items.Add(profile);
        if (repository.LoadWarnings.Count > 0) _message = $"{repository.LoadWarnings.Count} invalid custom profile file(s) were skipped.";
        Selected = Items.FirstOrDefault(profile => profile.Id == lastProfileId) ?? Items.FirstOrDefault();
        NewCommand = new RelayCommand(_ => New()); DuplicateCommand = new RelayCommand(_ => Duplicate());
        DeleteCommand = new RelayCommand(_ => Delete()); SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        ImportCommand = new AsyncRelayCommand(_ => ImportAsync()); ExportCommand = new AsyncRelayCommand(_ => ExportAsync());
    }

    public ObservableCollection<MdbProfile> Items { get; } = [];
    public ProfileEditorViewModel Editor { get; } = new();
    public IReadOnlyList<MdbFeatureLevel> FeatureLevels { get; } = Enum.GetValues<MdbFeatureLevel>();
    public IReadOnlyList<CapabilityStatus> CapabilityStatuses { get; } = Enum.GetValues<CapabilityStatus>();
    public ICommand NewCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public MdbProfile? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value) || value is null) return;
            Editor.Load(value); RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(Level));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public string Name => Selected?.Name ?? "None";
    public string Level => Selected?.BaseLevel.ToString() ?? "Unknown";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string ImportPath { get => _importPath; set => SetProperty(ref _importPath, value); }

    private void New()
    {
        var profile = new MdbProfile { Id = Guid.NewGuid().ToString("N"), Name = "New custom profile", BaseLevel = MdbFeatureLevel.Custom };
        Items.Add(profile); Selected = profile; Message = "New custom profile ready to edit.";
    }
    private void Duplicate()
    {
        if (Selected is null) return;
        var copy = Selected with { Id = Guid.NewGuid().ToString("N"), Name = Selected.Name + " Copy", IsBuiltIn = false };
        Items.Add(copy); Selected = copy; Message = "Profile duplicated as a custom profile.";
    }
    private void Delete()
    {
        if (Selected is null) return;
        try { _repository.DeleteCustom(Selected); Items.Remove(Selected); Selected = Items.FirstOrDefault(); Message = "Custom profile deleted."; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException) { Message = exception.Message; }
    }
    private async Task SaveAsync()
    {
        try
        {
            var updated = Editor.ToProfile();
            if (updated.IsBuiltIn) throw new InvalidOperationException("Built-in profiles are read-only. Duplicate one to customize it.");
            await _repository.SaveCustomAsync(updated);
            var index = Selected is null ? -1 : Items.IndexOf(Selected);
            if (index >= 0) Items[index] = updated; else Items.Add(updated);
            Selected = updated; Message = "Custom profile saved.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { Message = exception.Message; }
    }
    private async Task ImportAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ImportPath)) throw new InvalidOperationException("Enter the path of a profile JSON file.");
            var imported = await _repository.ImportAsync(ImportPath); Items.Add(imported); Selected = imported; Message = "Profile imported as custom.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { Message = exception.Message; }
    }
    private async Task ExportAsync()
    {
        try { if (Selected is null) throw new InvalidOperationException("Select a profile first."); Message = $"Profile exported to {await _repository.ExportAsync(Selected)}"; }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException) { Message = exception.Message; }
    }
}
