using MdbTestBench.Core.Protocol;
using MdbTestBench.Core.Protocol.Cashless;

namespace MdbTestBench.Core.Tests;

public sealed class MdbCashlessDecoderTests
{
    private readonly MdbCashlessDecoder _decoder = new();

    [Fact]
    public void DistinguishesAckFromJustReset()
    {
        Assert.IsType<MdbAckResponse>(_decoder.DecodeResponse([0x00]));
        Assert.IsType<MdbJustResetResponse>(_decoder.DecodeResponse([0x00, 0x00]));
        Assert.IsType<MdbNakResponse>(_decoder.DecodeResponse([0xFF]));
    }

    [Fact]
    public void DecodesReaderConfigData()
    {
        var decoded = Assert.IsType<MdbReaderConfigResponse>(_decoder.DecodeResponse(
            [0x01, 0x01, 0x18, 0x40, 0x01, 0x02, 0x05, 0x0F, 0x71]));

        Assert.Equal(MdbFeatureLevel.Level1, decoded.ReaderFeatureLevel);
        Assert.Equal(840, decoded.CountryOrCurrencyCode.IsoNumericCode);
        Assert.Equal(1, decoded.ScaleFactor);
        Assert.Equal(2, decoded.DecimalPlaces);
        Assert.Equal(0x0F, decoded.MiscellaneousOptions);
    }

    [Fact]
    public void DecodesLevel1SessionAndVendResponses()
    {
        var session = Assert.IsType<MdbBeginSessionResponse>(_decoder.DecodeResponse([0x03, 0x03, 0xE8, 0xEE]));
        var approved = Assert.IsType<MdbVendApprovedResponse>(_decoder.DecodeResponse([0x05, 0x01, 0xF4, 0xFA]));
        Assert.Equal(1_000UL, session.FundsAvailable);
        Assert.Equal(500UL, approved.VendAmount);
        Assert.IsType<MdbVendDeniedResponse>(_decoder.DecodeResponse([0x06, 0x06]));
        Assert.IsType<MdbEndSessionResponse>(_decoder.DecodeResponse([0x07, 0x07]));
        Assert.IsType<MdbCancelledResponse>(_decoder.DecodeResponse([0x08, 0x08]));
    }

    [Fact]
    public void DecodesLevel2RevalueResponses()
    {
        Assert.IsType<MdbRevalueApprovedResponse>(_decoder.DecodeResponse([0x0D, 0x0D]));
        Assert.IsType<MdbRevalueDeniedResponse>(_decoder.DecodeResponse([0x0E, 0x0E]));
        var limit = Assert.IsType<MdbRevalueLimitResponse>(_decoder.DecodeResponse(
            [0x0F, 0x03, 0xE8, 0xFA], new MdbCashlessDecodeOptions(MdbFeatureLevel.Level2)));
        Assert.Equal(1_000UL, limit.Amount);
    }

    [Fact]
    public void DecodesDisplaySessionCancelAndErrorResponses()
    {
        var display = Assert.IsType<MdbDisplayRequestResponse>(
            _decoder.DecodeResponse([0x02, 0x0A, 0x41, 0x42, 0x8F]));
        Assert.Equal(10, display.DisplayTimeTenths);
        Assert.Equal("AB", display.DisplayData);
        Assert.IsType<MdbSessionCancelRequestResponse>(_decoder.DecodeResponse([0x04, 0x04]));
        Assert.Equal(0xC1, Assert.IsType<MdbMalfunctionResponse>(
            _decoder.DecodeResponse([0x0A, 0xC1, 0xCB])).ErrorCode);
        Assert.Equal((byte?)0x04, Assert.IsType<MdbCommandOutOfSequenceResponse>(
            _decoder.DecodeResponse([0x0B, 0x04, 0x0F])).ReaderState);
    }

    [Fact]
    public void DecodesPeripheralIdAndLevel3Options()
    {
        var bytes = MdbCashlessResponseEncoder.PeripheralId(
            "ABC", "000000000001", "MODEL0000001", 0x0102,
            MdbLevel3Options.RemoteVend | MdbLevel3Options.Coupon);
        var response = Assert.IsType<MdbPeripheralIdResponse>(_decoder.DecodeResponse(bytes.Span));
        Assert.Equal("ABC", response.ManufacturerCode);
        Assert.Equal("000000000001", response.SerialNumber);
        Assert.Equal(MdbLevel3Options.RemoteVend | MdbLevel3Options.Coupon, response.OptionalFeatures);
    }

    [Fact]
    public void DecodesExpandedLevel3MonetaryResponsesOnlyWhenNegotiated()
    {
        var bytes = MdbCashlessResponseEncoder.EncodeData([0x05, 0x00, 0x01, 0x00, 0x00]);
        Assert.IsType<MdbMalformedCashlessResponse>(_decoder.DecodeResponse(bytes.Span));
        var decoded = Assert.IsType<MdbVendApprovedResponse>(_decoder.DecodeResponse(bytes.Span,
            new MdbCashlessDecodeOptions(MdbFeatureLevel.Level3, true)));
        Assert.Equal(65_536UL, decoded.VendAmount);
        Assert.True(decoded.ExpandedCurrencyMode);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x05 })]
    [InlineData(new byte[] { 0x05, 0x00 })]
    [InlineData(new byte[] { 0x06, 0xAA, 0xB0 })]
    public void MalformedOrIncompleteResponsesAreControlled(byte[] bytes) =>
        Assert.IsType<MdbMalformedCashlessResponse>(_decoder.DecodeResponse(bytes));

    [Fact]
    public void PreservesUnknownResponsePayload()
    {
        var response = Assert.IsType<MdbUnknownCashlessResponse>(_decoder.DecodeResponse([0x42, 0x99, 0xDB]));
        Assert.Equal(0x42, response.ResponseCode);
        Assert.Equal(new byte[] { 0x99 }, response.RawPayload.ToArray());
        Assert.Equal(new byte[] { 0x42, 0x99, 0xDB }, response.RawBytes.ToArray());
    }

    [Fact]
    public void PreservesUnknownExpansionResponse()
    {
        var bytes = MdbCashlessResponseEncoder.EncodeData([0xFF, 0x12, 0x34]);
        var response = Assert.IsType<UnknownExpansionResponse>(_decoder.DecodeResponse(bytes.Span));
        Assert.Equal(new byte[] { 0x12, 0x34 }, response.RawPayload.ToArray());
    }

    [Fact]
    public void PreservesKnownButNotImplementedResponseFamily()
    {
        var response = Assert.IsType<MdbUnknownCashlessResponse>(_decoder.DecodeResponse([0x11, 0x11]));
        Assert.Equal(0x11, response.ResponseCode);
        Assert.Equal(new byte[] { 0x11, 0x11 }, response.RawBytes.ToArray());
    }
}
