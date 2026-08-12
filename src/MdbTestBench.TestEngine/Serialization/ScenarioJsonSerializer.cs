using System.Text.Json;
using System.Text.Json.Serialization;
using MdbTestBench.TestEngine.Models;

namespace MdbTestBench.TestEngine.Serialization;

public sealed class ScenarioJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public TestScenario Deserialize(string json) =>
        JsonSerializer.Deserialize<TestScenario>(json, Options)
        ?? throw new JsonException("Scenario JSON did not contain an object.");

    public string Serialize(TestScenario scenario) => JsonSerializer.Serialize(scenario, Options);
}
