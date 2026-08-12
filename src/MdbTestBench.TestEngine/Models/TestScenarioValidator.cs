using MdbTestBench.Core.Protocol.Encoding;

namespace MdbTestBench.TestEngine.Models;

public static class TestScenarioValidator
{
    public const int MaxSteps = 1_000;
    public static readonly TimeSpan MaxTimeout = TimeSpan.FromHours(1);

    public static IReadOnlyList<string> Validate(TestScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(scenario.Id)) errors.Add("Scenario ID is required.");
        if (string.IsNullOrWhiteSpace(scenario.Name)) errors.Add("Scenario name is required.");
        else if (scenario.Name.Length > 200) errors.Add("Scenario name must contain at most 200 characters.");
        if (scenario.Timeout <= TimeSpan.Zero || scenario.Timeout > MaxTimeout)
            errors.Add($"Scenario timeout must be greater than zero and at most {MaxTimeout}.");
        if (scenario.Steps is null) errors.Add("Scenario steps are required.");
        else if (scenario.Steps.Count is 0 or > MaxSteps)
            errors.Add($"Scenario must contain between 1 and {MaxSteps} steps.");

        if (scenario.Steps is not null)
        {
            foreach (var step in scenario.Steps)
            {
                if (step is null)
                {
                    errors.Add("Scenario steps cannot contain null entries.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(step.Name)) errors.Add("Every scenario step requires a name.");
                if (!string.IsNullOrWhiteSpace(step.PayloadHex))
                {
                    var parsed = HexParser.Parse(step.PayloadHex);
                    if (!parsed.IsValid) errors.Add($"Step '{step.Name}' has invalid HEX: {parsed.Error}");
                }
            }
        }
        return errors;
    }

    public static void EnsureValid(TestScenario scenario)
    {
        var errors = Validate(scenario);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
    }
}
