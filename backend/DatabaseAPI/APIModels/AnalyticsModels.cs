namespace DatabaseAPI.APIModels;

public class AnalyticsSummaryDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public AnalyticsSlotsDto Slots { get; set; } = new();
    public AnalyticsVisitsDto Visits { get; set; } = new();
    /// <summary>Reserved slots in the past without a linked visit (proxy for no-show).</summary>
    public int NoShowProxy { get; set; }
    public AnalyticsEvaluationsDto Evaluations { get; set; } = new();
    public List<AnalyticsDayDto> Daily { get; set; } = new();
    public List<AnalyticsHospitalDto> ByHospital { get; set; } = new();
    public List<AnalyticsDoctorDto> ByDoctor { get; set; } = new();
}

public class AnalyticsSlotsDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public int Blocked { get; set; }
    public int Reserved { get; set; }
    public int Unavailable { get; set; }
    public int Booked => Blocked + Reserved;
    public double UtilizationPercent { get; set; }
}

public class AnalyticsVisitsDto
{
    public int Total { get; set; }
    public int WithIntake { get; set; }
    public int WithoutIntake { get; set; }
    public double IntakeCompletionPercent { get; set; }
}

public class AnalyticsEvaluationsDto
{
    public int Total { get; set; }
    public int Unreviewed { get; set; }
    public int PartiallyReviewed { get; set; }
    public int Completed { get; set; }
    public double? AverageHoursToFirstReview { get; set; }
}

public class AnalyticsDayDto
{
    public DateTime Date { get; set; }
    public int SlotsBooked { get; set; }
    public int Visits { get; set; }
    public int IntakeCompleted { get; set; }
    public int EvaluationsCompleted { get; set; }
}

public class AnalyticsHospitalDto
{
    public int HospitalId { get; set; }
    public string HospitalName { get; set; } = string.Empty;
    public int Slots { get; set; }
    public int Booked { get; set; }
    public int Visits { get; set; }
    public int IntakeCompleted { get; set; }
    public int Evaluations { get; set; }
}

public class AnalyticsDoctorDto
{
    public int? DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int BookedSlots { get; set; }
    public int EvaluationsCompleted { get; set; }
}
