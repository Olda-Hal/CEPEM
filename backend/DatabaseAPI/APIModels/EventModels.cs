using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.APIModels;

public class CreateEventRequest
{
    [Required]
    public int PatientId { get; set; }

    [Required]
    public int EventTypeId { get; set; }

    [Required]
    public DateTime HappenedAt { get; set; }

    public DateTime? HappenedTo { get; set; }

    public string? Comment { get; set; }

    public List<int> DrugIds { get; set; } = new List<int>();

    public List<int> ExaminationTypeIds { get; set; } = new List<int>();

    public List<int> SymptomIds { get; set; } = new List<int>();

    public List<int> InjuryTypeIds { get; set; } = new List<int>();

    public List<int> VaccineTypeIds { get; set; } = new List<int>();

    public bool? IsPregnant { get; set; }

    public bool? PregnancyResult { get; set; }
}

public class EventTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DrugResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ExaminationTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SymptomResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class InjuryTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class VaccineTypeResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EventOptionsResponse
{
    public List<EventTypeResponse> EventTypes { get; set; } = new List<EventTypeResponse>();
    public List<DrugResponse> Drugs { get; set; } = new List<DrugResponse>();
    public List<ExaminationTypeResponse> ExaminationTypes { get; set; } = new List<ExaminationTypeResponse>();
    public List<SymptomResponse> Symptoms { get; set; } = new List<SymptomResponse>();
    public List<InjuryTypeResponse> InjuryTypes { get; set; } = new List<InjuryTypeResponse>();
    public List<VaccineTypeResponse> VaccineTypes { get; set; } = new List<VaccineTypeResponse>();
}
