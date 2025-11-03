using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

// ============================================================================
// MCP SERVER TOOLS
// ============================================================================
[McpServerToolType]
public class RiskScorerTools
{
    [McpServerTool]
    [Description("Compute a clinical risk score from age and comorbidity count.")]
    public string CalculateRiskScore(
        [Description("Age of the patient")] int age,
        [Description("Number of chronic conditions")] int comorbidityCount)
    {
        var score = age * 0.2 + comorbidityCount * 5;
        var category = score switch
        {
            > 60 => "high",
            > 30 => "medium",
            _ => "low"
        };

        Console.WriteLine($"\n🔧 Tool called: calculate_risk_score");
        Console.WriteLine($"📝 Parameters: age={age}, comorbidityCount={comorbidityCount}");
        Console.WriteLine($"✅ Tool executed successfully");

        return JsonSerializer.Serialize(new { score, category }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Retrieve known health conditions for a patient.")]
    public string GetPatientHealthConditions(
        [Description("Patient ID")] string patientId)
    {
        Console.WriteLine($"\n🔧 Tool called: get_patient_health_conditions");
        Console.WriteLine($"📝 Parameters: patientId={patientId}");

        if (!PatientDatabase.HealthConditions.TryGetValue(patientId, out var conditions) || conditions.Count == 0)
        {
            Console.WriteLine($"✅ Tool executed successfully");
            return JsonSerializer.Serialize(new { patientId, message = "No conditions found." }, new JsonSerializerOptions { WriteIndented = true });
        }

        Console.WriteLine($"✅ Tool executed successfully");
        return JsonSerializer.Serialize(new { patientId, conditions }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Return demographic and condition summary for a patient.")]
    public string GetPatientSummary(
        [Description("Patient ID")] string patientId)
    {
        Console.WriteLine($"\n🔧 Tool called: get_patient_summary");
        Console.WriteLine($"📝 Parameters: patientId={patientId}");

        if (!PatientDatabase.Patients.TryGetValue(patientId, out var patient))
        {
            Console.WriteLine($"✅ Tool executed successfully");
            return $"No patient found with ID {patientId}";
        }

        var conditions = PatientDatabase.HealthConditions.GetValueOrDefault(patientId, new List<HealthCondition>());
        var result = new
        {
            patient.Name,
            patient.Age,
            patient.Comorbidities,
            Conditions = conditions
        };

        Console.WriteLine($"✅ Tool executed successfully");
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
