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

    public TestScenario Deserialize(string json)
    {
        var scenario = JsonSerializer.Deserialize<TestScenario>(json, Options)
            ?? throw new JsonException("Scenario JSON did not contain an object.");
        TestScenarioValidator.EnsureValid(scenario);
        return scenario;
    }

    public string Serialize(TestScenario scenario)
    {
        TestScenarioValidator.EnsureValid(scenario);
        return JsonSerializer.Serialize(scenario, Options);
    }
}
