using System.Collections.ObjectModel;
using System.Windows.Input;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App.ViewModels;

public sealed class ProfilesViewModel : ViewModelBase
{
    public event EventHandler? SelectionChanged;
    private readonly ProfileRepository _repository;
    private readonly ILocalizationService _localization;
    private MdbProfile? _selected;
    private string _message;
    private string _importPath = string.Empty;

    public ProfilesViewModel(ProfileRepository repository, string lastProfileId, ILocalizationService? localization = null)
    {
        _repository = repository; _localization = localization ?? new LocalizationService();
        _message = _localization.Get("BuiltInProfilesReadOnly");
        foreach (var status in Enum.GetValues<CapabilityStatus>())
            CapabilityStatusOptions.Add(new(status, _localization.Get("Capability" + status)));
        foreach (var profile in repository.LoadAll()) Items.Add(profile);
        if (repository.LoadWarnings.Count > 0) _message = _localization.Format("InvalidProfilesSkipped", repository.LoadWarnings.Count);
        Selected = Items.FirstOrDefault(profile => profile.Id == lastProfileId) ?? Items.FirstOrDefault();
        NewCommand = new RelayCommand(_ => New()); DuplicateCommand = new RelayCommand(_ => Duplicate());
        DeleteCommand = new RelayCommand(_ => Delete()); SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        ImportCommand = new AsyncRelayCommand(_ => ImportAsync()); ExportCommand = new AsyncRelayCommand(_ => ExportAsync());
    }

    public ObservableCollection<MdbProfile> Items { get; } = [];
    public ProfileEditorViewModel Editor { get; } = new();
    public IReadOnlyList<MdbFeatureLevel> FeatureLevels { get; } = Enum.GetValues<MdbFeatureLevel>();
    public ObservableCollection<LocalizedOption<CapabilityStatus>> CapabilityStatusOptions { get; } = [];
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
            Editor.Load(LocalizeBuiltIn(value)); RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(Level));
            RaiseCapabilitySelections();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public string Name => Selected?.Name ?? _localization.Get("None");
    public string Level => Selected?.BaseLevel.ToString() ?? _localization.Get("Unknown");
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string ImportPath { get => _importPath; set => SetProperty(ref _importPath, value); }
    public LocalizedOption<CapabilityStatus> ExpansionOption { get => Find(Editor.Expansion); set { if (value is not null) Editor.Expansion = value.Value; } }
    public LocalizedOption<CapabilityStatus> RevalueOption { get => Find(Editor.Revalue); set { if (value is not null) Editor.Revalue = value.Value; } }
    public LocalizedOption<CapabilityStatus> RemoteVendOption { get => Find(Editor.RemoteVend); set { if (value is not null) Editor.RemoteVend = value.Value; } }
    public LocalizedOption<CapabilityStatus> MultiCurrencyOption { get => Find(Editor.MultiCurrency); set { if (value is not null) Editor.MultiCurrency = value.Value; } }
    public LocalizedOption<CapabilityStatus> NegativeVendOption { get => Find(Editor.NegativeVend); set { if (value is not null) Editor.NegativeVend = value.Value; } }
    public LocalizedOption<CapabilityStatus> DataEntryOption { get => Find(Editor.DataEntry); set { if (value is not null) Editor.DataEntry = value.Value; } }
    public LocalizedOption<CapabilityStatus> BasketOption { get => Find(Editor.Basket); set { if (value is not null) Editor.Basket = value.Value; } }
    public LocalizedOption<CapabilityStatus> RefundOption { get => Find(Editor.Refund); set { if (value is not null) Editor.Refund = value.Value; } }

    private void New()
    {
        var profile = new MdbProfile { Id = Guid.NewGuid().ToString("N"), Name = _localization.Get("NewCustomProfile"), BaseLevel = MdbFeatureLevel.Custom };
        Items.Add(profile); Selected = profile; Message = _localization.Get("NewProfileReady");
    }
    private void Duplicate()
    {
        if (Selected is null) return;
        var copy = Selected with { Id = Guid.NewGuid().ToString("N"), Name = _localization.Format("ProfileCopyName", Selected.Name), IsBuiltIn = false };
        Items.Add(copy); Selected = copy; Message = _localization.Get("ProfileDuplicated");
    }
    private void Delete()
    {
        if (Selected is null) return;
        try { _repository.DeleteCustom(Selected); Items.Remove(Selected); Selected = Items.FirstOrDefault(); Message = _localization.Get("ProfileDeleted"); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException) { Message = _localization.Get("ProfileOperationFailed"); }
    }
    private async Task SaveAsync()
    {
        try
        {
            var updated = Editor.ToProfile();
            if (updated.IsBuiltIn) throw new InvalidOperationException();
            await _repository.SaveCustomAsync(updated);
            var index = Selected is null ? -1 : Items.IndexOf(Selected);
            if (index >= 0) Items[index] = updated; else Items.Add(updated);
            Selected = updated; Message = _localization.Get("ProfileSaved");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { Message = _localization.Get("ProfileOperationFailed"); }
    }
    private async Task ImportAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ImportPath)) throw new InvalidOperationException();
            var imported = await _repository.ImportAsync(ImportPath); Items.Add(imported); Selected = imported; Message = _localization.Get("ProfileImported");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { Message = _localization.Get("ProfileImportFailed"); }
    }
    private async Task ExportAsync()
    {
        try { if (Selected is null) throw new InvalidOperationException(); Message = _localization.Format("ProfileExported", await _repository.ExportAsync(Selected)); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException) { Message = _localization.Get("ProfileExportFailed"); }
    }

    public void RefreshLocalization()
    {
        Message = _localization.Get("BuiltInProfilesReadOnly");
        foreach (var option in CapabilityStatusOptions) option.Relocalize(_localization.Get("Capability" + option.Value));
        if (Selected is not null) Editor.Load(LocalizeBuiltIn(Selected));
        RaisePropertyChanged(nameof(Name)); RaisePropertyChanged(nameof(Level));
    }

    private MdbProfile LocalizeBuiltIn(MdbProfile profile) => profile.IsBuiltIn
        ? profile with { Description = _localization.Get("ProfileDescription_" + profile.Id.Replace('-', '_')) }
        : profile;

    private LocalizedOption<CapabilityStatus> Find(CapabilityStatus status) => CapabilityStatusOptions.Single(item => item.Value == status);
    private void RaiseCapabilitySelections()
    {
        RaisePropertyChanged(nameof(ExpansionOption)); RaisePropertyChanged(nameof(RevalueOption));
        RaisePropertyChanged(nameof(RemoteVendOption)); RaisePropertyChanged(nameof(MultiCurrencyOption));
        RaisePropertyChanged(nameof(NegativeVendOption)); RaisePropertyChanged(nameof(DataEntryOption));
        RaisePropertyChanged(nameof(BasketOption)); RaisePropertyChanged(nameof(RefundOption));
    }
}
