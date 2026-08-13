using System.Globalization;
using System.Windows.Input;
using MdbTestBench.App.Services;
using MdbTestBench.Core.Logging;
using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;
using MdbTestBench.Core.Protocol.Commands;
using MdbTestBench.Core.Protocol.Encoding;
using MdbTestBench.App.Localization;

namespace MdbTestBench.App.ViewModels;

public sealed class ManualViewModel : ViewModelBase
{
    private readonly WorkbenchSession _session;
    private readonly Func<bool> _isConnected;
    private readonly Func<bool> _isHardware;
    private readonly Func<MdbFeatureLevel> _profileLevel;
    private readonly Action _sessionChanged;
    private readonly ILocalizationService _localization;
    private string _inputCultureName;
    private ManualCommandKind _selectedCommand;
    private MdbCashlessDevice _selectedDevice = MdbCashlessDevice.CashlessDevice1;
    private string _price;
    private string _product = "1";
    private string _value = string.Empty;
    private string _preview = string.Empty;
    private string _message;
    private string _rawHex = string.Empty;
    private string _rawValidation;
    private bool _rawConfirmed;
    private string _lastCommand = "—";
    private string _lastResponse = "—";

    public ManualViewModel(WorkbenchSession session, Func<bool> isConnected, Func<bool> isHardware,
        Func<MdbFeatureLevel> profileLevel, Action sessionChanged, ILocalizationService? localization = null)
    {
        _session = session; _isConnected = isConnected; _isHardware = isHardware;
        _profileLevel = profileLevel; _sessionChanged = sessionChanged; _localization = localization ?? new LocalizationService();
        _inputCultureName = _localization.CurrentCulture.Name;
        _price = 5m.ToString("0.00", _localization.CurrentCulture);
        _message = _localization.Get("Ready"); _rawValidation = _localization.Get("EnterBytesToValidate");
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
    public string CurrentState => _localization.Get("State" + _session.State.Replace(" / codec unverified", "CodecUnverified", StringComparison.Ordinal));

    public void Refresh()
    {
        try
        {
            var command = Build();
            Preview = $"{command.Frame.Command} {command.Frame.Subcommand}\n{LocalizePayload(command)}\n\nMDB: {MdbLogFormatter.FormatHex(command.MdbBytes.Span)}";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) { Preview = _localization.Get("InvalidCommandValues"); }
        RaisePropertyChanged(nameof(CanUseStructured)); RaisePropertyChanged(nameof(CanUseRaw)); RaisePropertyChanged(nameof(CurrentState));
    }

    private async Task SendStructuredAsync()
    {
        try
        {
            if (!_isConnected()) throw new InvalidOperationException(_localization.Get("ConnectSimulatorFirst"));
            if (_isHardware()) throw new InvalidOperationException(_localization.Get("WaferCodecNotValidated"));
            var command = Build();
            if (!_session.CanSend(command.Frame)) throw new InvalidOperationException(_localization.Format("CommandBlocked", command.Trigger, CurrentState));
            LastCommand = command.Frame.Subcommand == MdbSubcommandType.None ? command.Frame.Command.ToString() : $"{command.Frame.Command} {command.Frame.Subcommand}";
            var response = await _session.ExchangeAsync(command.Frame);
            LastResponse = response.Response?.ToString() ?? _localization.Get("Unknown"); Message = _localization.Format("ReceivedResponse", LastResponse);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or TimeoutException) { Message = exception.Message; }
        finally { Refresh(); _sessionChanged(); }
    }

    private async Task SendRawAsync()
    {
        try
        {
            if (_isHardware()) throw new InvalidOperationException(_localization.Get("RawHardwareDiscoveryOnly"));
            if (!RawConfirmed) throw new InvalidOperationException(_localization.Get("ConfirmSimulatorRawError"));
            var parsed = HexParser.Parse(RawHex);
            if (!parsed.IsValid) throw new InvalidDataException(parsed.Error);
            var result = await _session.ExchangeRawAsync(parsed.Bytes);
            Message = _localization.Format("SimulatorRawResponse", MdbLogFormatter.FormatHex(result.ResponseBytes.Span)); RawConfirmed = false;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or TimeoutException) { Message = exception.Message; }
    }

    private ManualCommandBuildResult Build() => ManualCommandBuilder.Build(new ManualCommandInput(SelectedCommand,
        Parse(Price), int.TryParse(Product, NumberStyles.Integer, _localization.CurrentCulture, out var product) ? product : null,
        Parse(Value), SelectedDevice, _profileLevel() is MdbFeatureLevel.Level2 or MdbFeatureLevel.Level3 ? _profileLevel() : MdbFeatureLevel.Level1));

    private decimal? Parse(string value) => decimal.TryParse(value, NumberStyles.Number, _localization.CurrentCulture, out var result) ? result : null;
    private void ValidateRaw()
    {
        var result = HexParser.Parse(RawHex);
        RawValidation = result.IsValid ? _localization.Format("ValidBytes", result.Bytes.Length, result.NormalizedHex) : _localization.Get("InvalidHex");
        if (!result.IsValid) RawConfirmed = false;
    }

    private string LocalizePayload(ManualCommandBuildResult command) => command.SemanticCommand switch
    {
        MdbVendRequestCommand vend => _localization.Format("VendLogicalPayload", Parse(Price), vend.ItemPrice, vend.ItemNumber),
        MdbCashSaleCommand sale => _localization.Format("VendLogicalPayload", Parse(Price), sale.ItemPrice, sale.ItemNumber),
        MdbSetupMaxMinPricesCommand prices => _localization.Format("PriceRangeLogicalPayload",
            string.IsNullOrWhiteSpace(Price) ? _localization.Get("Unknown") : Price,
            string.IsNullOrWhiteSpace(Value) ? _localization.Get("Unknown") : Value,
            prices.MaximumPrice, prices.MinimumPrice),
        MdbRevalueRequestCommand revalue => _localization.Format("RevalueLogicalPayload", Parse(Value), revalue.Amount),
        _ => _localization.Get("NoLogicalFields")
    };

    public void RefreshLocalization()
    {
        var previousCulture = CultureInfo.GetCultureInfo(_inputCultureName);
        var price = decimal.TryParse(Price, NumberStyles.Number, previousCulture, out var parsedPrice) ? parsedPrice : (decimal?)null;
        var value = decimal.TryParse(Value, NumberStyles.Number, previousCulture, out var parsedValue) ? parsedValue : (decimal?)null;
        _inputCultureName = _localization.CurrentCulture.Name;
        if (price is not null) Price = price.Value.ToString("0.00", _localization.CurrentCulture);
        if (value is not null) Value = value.Value.ToString("0.00", _localization.CurrentCulture);
        Message = _localization.Get("Ready"); ValidateRaw(); Refresh();
    }
}
