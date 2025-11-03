// ============================================================================
// MOCK DATA MODELS
// ============================================================================
public class Patient
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public int Comorbidities { get; set; }
}

public class HealthCondition
{
    public string Name { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string DateDiagnosed { get; set; } = string.Empty;
}

public static class PatientDatabase
{
    public static readonly Dictionary<string, Patient> Patients = new()
    {
        { "P001", new Patient { Name = "Alice Johnson", Age = 68, Comorbidities = 3 } },
        { "P002", new Patient { Name = "Robert Smith", Age = 45, Comorbidities = 1 } },
        { "P003", new Patient { Name = "Maria Lopez", Age = 72, Comorbidities = 5 } }
    };

    public static readonly Dictionary<string, List<HealthCondition>> HealthConditions = new()
    {
        { "P001", new List<HealthCondition>
            {
                new() { Name = "Type 2 Diabetes", Severity = "moderate", DateDiagnosed = "2019-05-12" },
                new() { Name = "Hypertension", Severity = "mild", DateDiagnosed = "2015-09-03" }
            }
        },
        { "P002", new List<HealthCondition>
            {
                new() { Name = "Asthma", Severity = "mild", DateDiagnosed = "2002-11-22" }
            }
        },
        { "P003", new List<HealthCondition>
            {
                new() { Name = "Chronic Kidney Disease", Severity = "severe", DateDiagnosed = "2018-04-01" },
                new() { Name = "Coronary Artery Disease", Severity = "severe", DateDiagnosed = "2020-10-10" }
            }
        }
    };
}
