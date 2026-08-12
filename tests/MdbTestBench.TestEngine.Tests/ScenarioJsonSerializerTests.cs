using MdbTestBench.Core.Protocol;
using MdbTestBench.TestEngine.Models;
using MdbTestBench.TestEngine.Serialization;

namespace MdbTestBench.TestEngine.Tests;

public sealed class ScenarioJsonSerializerTests
{
    [Fact]
    public void Scenario_RoundTripsAsJson()
    {
        var serializer = new ScenarioJsonSerializer();
        var scenario = new TestScenario
        {
            Id = "json-test",
            Name = "JSON test",
            Steps = [new TestStep { Name = "Reset", Command = MdbCommandType.Reset, ExpectedResponse = MdbResponseType.JustReset }]
        };

        var json = serializer.Serialize(scenario);
        var result = serializer.Deserialize(json);

        Assert.Equal(scenario.Id, result.Id);
        Assert.Equal(MdbCommandType.Reset, result.Steps[0].Command);
        Assert.Contains("\"reset\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidScenarioJsonIsRejectedByDomainValidation()
    {
        const string json = """{ "id": "", "name": "", "timeout": "00:00:00", "steps": [] }""";

        Assert.Throws<InvalidDataException>(() => new ScenarioJsonSerializer().Deserialize(json));
    }
}
