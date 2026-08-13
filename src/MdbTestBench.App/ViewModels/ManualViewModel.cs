using System.Globalization;
using System.Windows.Input;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Protocol.Encoding;

namespace MdbTestBench.App.ViewModels;

public sealed class ManualViewModel : ViewModelBase
{
    private readonly WorkbenchSession _session;
    private readonly Func<bool> _isConnected;
    private readonly Func<bool> _isHardware;
    private readonly Func<MdbFeatureLevel> _profileLevel;
    private readonly Action _sessionChanged;
    private ManualCommandKind _selectedCommand;
    private MdbCashlessDevice _selectedDevice = MdbCashlessDevice.CashlessDevice1;
    private string _price = "5.00";
    private string _product = "1";
    private string _value = string.Empty;
    private string _preview = string.Empty;
    private string _message = "Ready";
    private string _rawHex = string.Empty;
    private string _rawValidation = "Enter bytes to validate.";
    private bool _rawConfirmed;
    private string _lastCommand = "—";
    private string _lastResponse = "—";

    public ManualViewModel(WorkbenchSession session, Func<bool> isConnected, Func<bool> isHardware,
        Func<MdbFeatureLevel> profileLevel, Action sessionChanged)
    {
        _session = session; _isConnected = isConnected; _isHardware = isHardware;
        _profileLevel = profileLevel; _sessionChanged = sessionChanged;
        SendStructuredCommand = new AsyncRelayCommand(_ => SendStructuredAsync());
        SendRawCommand = new AsyncRelayCommand(_ => SendRawAsync());
        Refresh();
    }

    public IReadOnlyList<ManualCommandKind> CommandOptions { get; } = Enum.GetValues<ManualCommandKind>();
    public IReadOnlyList<MdbCashlessDevice> DeviceOptions { get; } = Enum.GetValues<MdbCashlessDevice>();
    public ICommand SendStructuredCommand { get; }
    public ICommand SendRawCommand { get; }
    public ManualCommandKind SelectedCommand { get => _selectedCommand; set { if (SetProperty(ref _selectedCommand, value)) Refresh(); } }
    public MdbCashlessDevice SelectedDevice { get => _selectedDevice; set { if (SetProperty(ref _selectedDevice, value)) Refresh(); } }
    public string Price { get => _price; set { if (SetProperty(ref _price, value)) Refresh(); } }
    public string Product { get => _product; set { if (SetProperty(ref _product, value)) Refresh(); } }
    public string Value { get => _value; set { if (SetProperty(ref _value, value)) Refresh(); } }
    public string Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string RawHex { get => _rawHex; set { if (SetProperty(ref _rawHex, value)) ValidateRaw(); } }
    public string RawValidation { get => _rawValidation; private set => SetProperty(ref _rawValidation, value); }
    public bool RawConfirmed { get => _rawConfirmed; set => SetProperty(ref _rawConfirmed, value); }
    public string LastCommand { get => _lastCommand; private set => SetProperty(ref _lastCommand, value); }
    public string LastResponse { get => _lastResponse; private set => SetProperty(ref _lastResponse, value); }
    public bool CanUseStructured => _isConnected() && !_isHardware();
    public bool CanUseRaw => _isConnected() && !_isHardware();
    public string CurrentState => _session.State;

    public void Refresh()
    {
        try
        {
            var command = Build();
            Preview = $"{command.Frame.Command} {command.Frame.Subcommand}\n{command.LogicalPayload}\n\nMDB: {MdbLogFormatter.FormatHex(command.MdbBytes.Span)}";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { Preview = exception.Message; }
        RaisePropertyChanged(nameof(CanUseStructured)); RaisePropertyChanged(nameof(CanUseRaw)); RaisePropertyChanged(nameof(CurrentState));
    }

    private async Task SendStructuredAsync()
    {
        try
        {
            if (!_isConnected()) throw new InvalidOperationException("Connect the Simulator before sending a command.");
            if (_isHardware()) throw new InvalidOperationException("Wafer codec not validated. Use Adapter Discovery for hardware analysis.");
            var command = Build();
            if (!_session.CanSend(command.Frame)) throw new InvalidOperationException($"{command.Trigger} is blocked while the VMC is in {_session.State}.");
            LastCommand = command.Frame.Subcommand == MdbSubcommandType.None ? command.Frame.Command.ToString() : $"{command.Frame.Command} {command.Frame.Subcommand}";
            var response = await _session.ExchangeAsync(command.Frame);
            LastResponse = response.Response?.ToString() ?? "Unknown"; Message = $"Received {LastResponse}.";
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or TimeoutException) { Message = exception.Message; }
        finally { Refresh(); _sessionChanged(); }
    }

    private async Task SendRawAsync()
    {
        try
        {
            if (_isHardware()) throw new InvalidOperationException("Hardware Raw Adapter is available only in Wafer Discovery so TX is always captured.");
            if (!RawConfirmed) throw new InvalidOperationException("Confirm the simulator raw payload before sending.");
            var parsed = HexParser.Parse(RawHex);
            if (!parsed.IsValid) throw new InvalidDataException(parsed.Error);
            var result = await _session.ExchangeRawAsync(parsed.Bytes);
            Message = $"Simulator raw response: {MdbLogFormatter.FormatHex(result.ResponseBytes.Span)}"; RawConfirmed = false;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or TimeoutException) { Message = exception.Message; }
    }

    private ManualCommandBuildResult Build() => ManualCommandBuilder.Build(new ManualCommandInput(SelectedCommand,
        Parse(Price), int.TryParse(Product, NumberStyles.Integer, CultureInfo.InvariantCulture, out var product) ? product : null,
        Parse(Value), SelectedDevice, _profileLevel() is MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3 ? _profileLevel() : MdbFeatureLevel.Level1));

    private static decimal? Parse(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    private void ValidateRaw()
    {
        var result = HexParser.Parse(RawHex);
        RawValidation = result.IsValid ? $"Valid · {result.Bytes.Length} byte(s) · {result.NormalizedHex}" : result.Error ?? "Invalid";
        if (!result.IsValid) RawConfirmed = false;
    }
}
