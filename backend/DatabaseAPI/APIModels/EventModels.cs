using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.APIModels;

public class DrugUseRequest
{
    [Required]
    public int DrugId { get; set; }
    
    public List<int> CategoryIds { get; set; } = new List<int>();
}

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

    public Guid? EventGroupId { get; set; }

    public List<DrugUseRequest> DrugUses { get; set; } = new List<DrugUseRequest>();

    public List<int> ExaminationTypeIds { get; set; } = new List<int>();

    public List<int> SymptomIds { get; set; } = new List<int>();

    public List<int> InjuryTypeIds { get; set; } = new List<int>();

    public List<int> VaccineTypeIds { get; set; } = new List<int>();

    public bool? IsPregnant { get; set; }

    public bool? PregnancyResult { get; set; }
}

public class CreateEventGroupRequest
{
    [Required]
    public int PatientId { get; set; }

    public List<CreateEventRequest> Events { get; set; } = new List<CreateEventRequest>();
}

public class CreateEventGroupResponse
{
    public Guid EventGroupId { get; set; }
    public List<int> EventIds { get; set; } = new List<int>();
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

public class DrugCategoryResponse
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
    public List<DrugCategoryResponse> DrugCategories { get; set; } = new List<DrugCategoryResponse>();
    public List<ExaminationTypeResponse> ExaminationTypes { get; set; } = new List<ExaminationTypeResponse>();
    public List<SymptomResponse> Symptoms { get; set; } = new List<SymptomResponse>();
    public List<InjuryTypeResponse> InjuryTypes { get; set; } = new List<InjuryTypeResponse>();
    public List<VaccineTypeResponse> VaccineTypes { get; set; } = new List<VaccineTypeResponse>();
}
