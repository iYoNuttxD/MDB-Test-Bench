using System.Collections.ObjectModel;
using MdbTestBench.Core.Capabilities;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Profiles;
using MdbTestBench.Core.Protocol;
using MdbTestBench.TestEngine.Models;
using System.Globalization;

namespace MdbTestBench.App.ViewModels;

public sealed class LogEntryViewModel
{
    public LogEntryViewModel(MdbLogEntry entry) => Entry = entry;

    public MdbLogEntry Entry { get; }
    public string Timestamp => Entry.Timestamp.ToString("HH:mm:ss.ffffff", CultureInfo.InvariantCulture);
    public string Direction => Entry.Direction.ToString().ToUpperInvariant();
    public string Command => Entry.Command;
    public string Description => Entry.DecodedDescription;
    public string RawHex => MdbLogFormatter.FormatHex(Entry.RawData.Span);
    public string Severity => Entry.Severity.ToString();
    public bool IsError => Entry.Severity >= MdbLogSeverity.Error;
    public string Line => $"{Timestamp} {Direction,-2} {Command,-22} {Description} RAW: {RawHex}";
}

public sealed class ProfileEditorViewModel : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _description = string.Empty;
    private MdbFeatureLevel _baseLevel = MdbFeatureLevel.Custom;
    private bool _isBuiltIn;
    private CapabilityStatus _expansion;
    private CapabilityStatus _revalue;
    private CapabilityStatus _remoteVend;
    private CapabilityStatus _multiCurrency;
    private CapabilityStatus _negativeVend;
    private CapabilityStatus _dataEntry;
    private CapabilityStatus _basket;
    private CapabilityStatus _refund;

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public MdbFeatureLevel BaseLevel { get => _baseLevel; set => SetProperty(ref _baseLevel, value); }
    public bool IsBuiltIn { get => _isBuiltIn; set { if (SetProperty(ref _isBuiltIn, value)) RaisePropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => !IsBuiltIn;
    public CapabilityStatus Expansion { get => _expansion; set => SetProperty(ref _expansion, value); }
    public CapabilityStatus Revalue { get => _revalue; set => SetProperty(ref _revalue, value); }
    public CapabilityStatus RemoteVend { get => _remoteVend; set => SetProperty(ref _remoteVend, value); }
    public CapabilityStatus MultiCurrency { get => _multiCurrency; set => SetProperty(ref _multiCurrency, value); }
    public CapabilityStatus NegativeVend { get => _negativeVend; set => SetProperty(ref _negativeVend, value); }
    public CapabilityStatus DataEntry { get => _dataEntry; set => SetProperty(ref _dataEntry, value); }
    public CapabilityStatus Basket { get => _basket; set => SetProperty(ref _basket, value); }
    public CapabilityStatus Refund { get => _refund; set => SetProperty(ref _refund, value); }

    public void Load(MdbProfile profile)
    {
        Id = profile.Id;
        Name = profile.Name;
        Description = profile.Description;
        BaseLevel = profile.BaseLevel;
        IsBuiltIn = profile.IsBuiltIn;
        Expansion = profile.Capabilities.Expansion;
        Revalue = profile.Capabilities.Revalue;
        RemoteVend = profile.Capabilities.RemoteVend;
        MultiCurrency = profile.Capabilities.MultiCurrency;
        NegativeVend = profile.Capabilities.NegativeVend;
        DataEntry = profile.Capabilities.DataEntry;
        Basket = profile.Capabilities.Basket;
        Refund = profile.Capabilities.Refund;
    }

    public MdbProfile ToProfile() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        BaseLevel = BaseLevel,
        IsBuiltIn = IsBuiltIn,
        Capabilities = new MdbCapabilities
        {
            Expansion = Expansion,
            Revalue = Revalue,
            RemoteVend = RemoteVend,
            MultiCurrency = MultiCurrency,
            NegativeVend = NegativeVend,
            DataEntry = DataEntry,
            Basket = Basket,
            Refund = Refund
        }
    };
}

public sealed class ScenarioDisplayViewModel(TestScenario scenario) : ViewModelBase
{
    private string _result = "NOT RUN";
    private string _duration = "—";
    public TestScenario Scenario { get; } = scenario;
    public string Name => Scenario.Name;
    public string Description => Scenario.Description ?? string.Empty;
    public string RequiredProfile => Scenario.RequiredProfile.ToString();
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public ObservableCollection<ScenarioStepDisplayViewModel> Steps { get; } =
        new(scenario.Steps.Select((step, index) => new ScenarioStepDisplayViewModel(index + 1, step)));
}

public sealed class ScenarioStepDisplayViewModel(int number, TestStep step) : ViewModelBase
{
    private string _status = "PENDING";
    private string _received = "—";
    public int Number { get; } = number;
    public string NumberText => Number.ToString("D2", CultureInfo.InvariantCulture);
    public string Name => step.Name;
    public string Expected => step.ExpectedResponse.ToString();
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Received { get => _received; set => SetProperty(ref _received, value); }
}

public sealed record ProtocolSupportViewModel(
    string FeatureLevel,
    string Encoder,
    string Decoder,
    string Simulator,
    string Hardware);
