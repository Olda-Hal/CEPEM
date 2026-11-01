using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using DatabaseAPI.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Clinic-level business metrics. Scope follows the acting admin:
/// Center Admin sees own centers, Country Admin all centers in administered
/// countries, SysAdmin/Admin everything (optionally one hospital).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly IEmployeeScopeService _employeeScopeService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        DatabaseContext context,
        IEmployeeScopeService employeeScopeService,
        ILogger<AnalyticsController> logger)
    {
        _context = context;
        _employeeScopeService = employeeScopeService;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AnalyticsSummaryDto>> GetSummary(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? hospitalId = null)
    {
        try
        {
            var now = DateTime.UtcNow;
            var toUtc = to?.Date.AddDays(1) ?? now.Date.AddDays(1);
            var fromUtc = from?.Date ?? toUtc.AddDays(-30).Date;
            if (fromUtc >= toUtc)
            {
                return BadRequest("The 'from' date must be before 'to'");
            }
            if ((toUtc - fromUtc).TotalDays > 366)
            {
                return BadRequest("The date range must be at most 366 days");
            }

            var actor = await _employeeScopeService.GetActorScopeAsync();
            if (!actor.HasFullAccess && !actor.IsScopedAdmin)
            {
                return StatusCode(403, "K tomuto přehledu nemáte přístup.");
            }

            HashSet<int>? allowedHospitals = null;
            if (!actor.HasFullAccess)
            {
                if (actor.IsCenterAdmin)
                {
                    allowedHospitals = actor.HospitalIds.ToHashSet();
                }
                else if (actor.IsCountryAdmin)
                {
                    allowedHospitals = (await _context.Hospitals
                        .Where(hospital => actor.AdministeredCountryScopeIds.Contains(hospital.CountryScopeId))
                        .Select(hospital => hospital.Id)
                        .ToListAsync())
                        .ToHashSet();
                }
            }

            var explicitHospitalFilter = hospitalId.HasValue;
            if (hospitalId.HasValue)
            {
                if (allowedHospitals != null && !allowedHospitals.Contains(hospitalId.Value))
                {
                    return StatusCode(403, "K tomuto centru nemáte přístup.");
                }
                allowedHospitals = new HashSet<int> { hospitalId.Value };
            }

            var slots = await _context.ReservationSlots
                .AsNoTracking()
                .Include(slot => slot.Hospital)
                .Include(slot => slot.Doctor)
                    .ThenInclude(doctor => doctor!.Person)
                .Where(slot => slot.StartDateTime >= fromUtc && slot.StartDateTime < toUtc)
                .ToListAsync();

            if (allowedHospitals != null)
            {
                slots = slots.Where(slot => allowedHospitals.Contains(slot.HospitalId)).ToList();
            }

            var visits = await _context.Visits
                .AsNoTracking()
                .Include(visit => visit.Patient)
                .Include(visit => visit.ReservationSlot)
                .Where(visit => visit.VisitDate >= fromUtc && visit.VisitDate < toUtc)
                .ToListAsync();
            visits = visits
                .Where(visit => VisitInScope(visit, allowedHospitals, actor, explicitHospitalFilter))
                .ToList();

            var evaluations = await _context.EvaluationCases
                .AsNoTracking()
                .Include(evaluation => evaluation.Patient)
                .Include(evaluation => evaluation.Reviews)
                .Where(evaluation => evaluation.CreatedAtUtc >= fromUtc && evaluation.CreatedAtUtc < toUtc)
                .ToListAsync();
            evaluations = evaluations
                .Where(evaluation => EvaluationInScope(evaluation, allowedHospitals, actor, explicitHospitalFilter))
                .ToList();

            var reservedPast = slots
                .Where(slot => slot.Status == SlotStatus.Reserved && slot.StartDateTime < now)
                .ToList();
            var visitSlotIds = visits
                .Where(visit => visit.ReservationSlotId.HasValue)
                .Select(visit => visit.ReservationSlotId!.Value)
                .ToHashSet();

            var result = new AnalyticsSummaryDto
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Slots = BuildSlotMetrics(slots),
                Visits = BuildVisitMetrics(visits),
                NoShowProxy = reservedPast.Count(slot => !visitSlotIds.Contains(slot.Id)),
                Evaluations = BuildEvaluationMetrics(evaluations),
                Daily = BuildDaily(slots, visits, evaluations, fromUtc, toUtc),
                ByHospital = await BuildByHospitalAsync(slots, visits, evaluations, allowedHospitals),
                ByDoctor = await BuildByDoctorAsync(slots, evaluations)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics summary failed");
            return StatusCode(500, "An error occurred while building the analytics summary");
        }
    }

    private static AnalyticsSlotsDto BuildSlotMetrics(List<ReservationSlot> slots)
    {
        var total = slots.Count;
        var unavailable = slots.Count(slot => slot.Status == SlotStatus.Unavailable);
        var available = slots.Count(slot => slot.Status == SlotStatus.Available);
        var blocked = slots.Count(slot => slot.Status == SlotStatus.Blocked);
        var reserved = slots.Count(slot => slot.Status == SlotStatus.Reserved);
        var denominator = total - unavailable;

        return new AnalyticsSlotsDto
        {
            Total = total,
            Available = available,
            Blocked = blocked,
            Reserved = reserved,
            Unavailable = unavailable,
            UtilizationPercent = denominator <= 0
                ? 0
                : Round1((blocked + reserved) * 100.0 / denominator)
        };
    }

    private static AnalyticsVisitsDto BuildVisitMetrics(List<Visit> visits)
    {
        var total = visits.Count;
        var withIntake = visits.Count(visit => visit.SourceEventId.HasValue);
        return new AnalyticsVisitsDto
        {
            Total = total,
            WithIntake = withIntake,
            WithoutIntake = total - withIntake,
            IntakeCompletionPercent = total == 0 ? 0 : Round1(withIntake * 100.0 / total)
        };
    }

    private static AnalyticsEvaluationsDto BuildEvaluationMetrics(List<EvaluationCase> evaluations)
    {
        var completed = evaluations.Count(evaluation => evaluation.Reviews.Count >= 2);
        var partial = evaluations.Count(evaluation => evaluation.Reviews.Count == 1);
        var unreviewed = evaluations.Count(evaluation => evaluation.Reviews.Count == 0);

        var durations = evaluations
            .Where(evaluation => evaluation.Reviews.Count > 0)
            .Select(evaluation => (evaluation.Reviews.Min(review => review.EvaluatedAtUtc) - evaluation.CreatedAtUtc).TotalHours)
            .Where(hours => hours >= 0)
            .ToList();

        return new AnalyticsEvaluationsDto
        {
            Total = evaluations.Count,
            Completed = completed,
            PartiallyReviewed = partial,
            Unreviewed = unreviewed,
            AverageHoursToFirstReview = durations.Count == 0 ? null : Round1(durations.Average())
        };
    }

    private static List<AnalyticsDayDto> BuildDaily(
        List<ReservationSlot> slots,
        List<Visit> visits,
        List<EvaluationCase> evaluations,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var slotGroups = slots
            .Where(slot => slot.Status is SlotStatus.Blocked or SlotStatus.Reserved)
            .GroupBy(slot => slot.StartDateTime.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var visitGroups = visits
            .GroupBy(visit => visit.VisitDate.Date)
            .ToDictionary(group => group.Key, group => group.ToList());
        var reviewGroups = evaluations
            .SelectMany(evaluation => evaluation.Reviews)
            .Where(review => review.EvaluatedAtUtc >= fromUtc && review.EvaluatedAtUtc < toUtc)
            .GroupBy(review => review.EvaluatedAtUtc.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var days = new List<AnalyticsDayDto>();
        for (var day = fromUtc.Date; day < toUtc.Date; day = day.AddDays(1))
        {
            var dayVisits = visitGroups.TryGetValue(day, out var value) ? value : new List<Visit>();
            days.Add(new AnalyticsDayDto
            {
                Date = day,
                SlotsBooked = slotGroups.TryGetValue(day, out var booked) ? booked : 0,
                Visits = dayVisits.Count,
                IntakeCompleted = dayVisits.Count(visit => visit.SourceEventId.HasValue),
                EvaluationsCompleted = reviewGroups.TryGetValue(day, out var reviewed) ? reviewed : 0
            });
        }

        return days;
    }

    private async Task<List<AnalyticsHospitalDto>> BuildByHospitalAsync(
        List<ReservationSlot> slots,
        List<Visit> visits,
        List<EvaluationCase> evaluations,
        HashSet<int>? allowedHospitals)
    {
        int? VisitHospitalId(Visit visit) =>
            visit.ReservationSlot?.HospitalId ?? visit.Patient?.HomeHospitalId;

        var hospitalIds = slots.Select(slot => slot.HospitalId)
            .Concat(visits.Select(VisitHospitalId).Where(id => id.HasValue).Select(id => id!.Value))
            .Concat(evaluations.Select(evaluation => evaluation.Patient?.HomeHospitalId)
                .Where(id => id.HasValue).Select(id => id!.Value))
            .Distinct()
            .ToList();

        if (allowedHospitals != null)
        {
            hospitalIds = hospitalIds.Where(allowedHospitals.Contains).ToList();
        }

        if (hospitalIds.Count == 0)
        {
            return new List<AnalyticsHospitalDto>();
        }

        var names = await _context.Hospitals
            .Where(hospital => hospitalIds.Contains(hospital.Id))
            .ToDictionaryAsync(hospital => hospital.Id, hospital => hospital.Name ?? $"#{hospital.Id}");

        return hospitalIds
            .Select(hospitalId =>
            {
                var hospitalSlots = slots.Where(slot => slot.HospitalId == hospitalId).ToList();
                var hospitalVisits = visits.Where(visit => VisitHospitalId(visit) == hospitalId).ToList();
                return new AnalyticsHospitalDto
                {
                    HospitalId = hospitalId,
                    HospitalName = names.TryGetValue(hospitalId, out var name) ? name : $"#{hospitalId}",
                    Slots = hospitalSlots.Count,
                    Booked = hospitalSlots.Count(slot => slot.Status is SlotStatus.Blocked or SlotStatus.Reserved),
                    Visits = hospitalVisits.Count,
                    IntakeCompleted = hospitalVisits.Count(visit => visit.SourceEventId.HasValue),
                    Evaluations = evaluations.Count(evaluation =>
                        evaluation.Patient?.HomeHospitalId == hospitalId)
                };
            })
            .OrderBy(row => row.HospitalName)
            .ToList();
    }

    private async Task<List<AnalyticsDoctorDto>> BuildByDoctorAsync(
        List<ReservationSlot> slots,
        List<EvaluationCase> evaluations)
    {
        var bookedSlots = slots
            .Where(slot => slot.DoctorId.HasValue && slot.Status is SlotStatus.Blocked or SlotStatus.Reserved)
            .GroupBy(slot => slot.DoctorId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var reviewCounts = evaluations
            .SelectMany(evaluation => evaluation.Reviews)
            .Where(review => review.EmployeeId.HasValue)
            .GroupBy(review => review.EmployeeId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var doctorIds = bookedSlots.Keys.Concat(reviewCounts.Keys).Distinct().ToList();
        if (doctorIds.Count == 0)
        {
            return new List<AnalyticsDoctorDto>();
        }

        var names = await _context.Employees
            .Where(doctor => doctorIds.Contains(doctor.Id))
            .Select(doctor => new
            {
                doctor.Id,
                Name = (doctor.Person.FirstName + " " + doctor.Person.LastName).Trim()
            })
            .ToDictionaryAsync(doctor => doctor.Id, doctor => doctor.Name);

        return doctorIds
            .Select(doctorId => new AnalyticsDoctorDto
            {
                DoctorId = doctorId,
                DoctorName = names.TryGetValue(doctorId, out var name) ? name : $"#{doctorId}",
                BookedSlots = bookedSlots.TryGetValue(doctorId, out var booked) ? booked : 0,
                EvaluationsCompleted = reviewCounts.TryGetValue(doctorId, out var reviews) ? reviews : 0
            })
            .OrderByDescending(row => row.BookedSlots)
            .ThenByDescending(row => row.EvaluationsCompleted)
            .ToList();
    }

    private static bool VisitInScope(
        Visit visit,
        HashSet<int>? allowedHospitals,
        ActorScope actor,
        bool explicitHospitalFilter)
    {
        if (allowedHospitals == null)
        {
            return true;
        }

        if (visit.ReservationSlot?.HospitalId is int slotHospital && allowedHospitals.Contains(slotHospital))
        {
            return true;
        }
        if (visit.Patient?.HomeHospitalId is int homeHospital && allowedHospitals.Contains(homeHospital))
        {
            return true;
        }
        return !explicitHospitalFilter &&
               actor.IsCountryAdmin &&
               actor.AdministeredCountryScopeIds.Contains(CountryScopeMapper.ToScopeId(visit.CountryCode));
    }

    private static bool EvaluationInScope(
        EvaluationCase evaluation,
        HashSet<int>? allowedHospitals,
        ActorScope actor,
        bool explicitHospitalFilter)
    {
        if (allowedHospitals == null)
        {
            return true;
        }
        if (evaluation.Patient == null)
        {
            return false;
        }
        if (evaluation.Patient.HomeHospitalId is int hospital && allowedHospitals.Contains(hospital))
        {
            return true;
        }
        return !explicitHospitalFilter &&
               actor.IsCountryAdmin &&
               actor.AdministeredCountryScopeIds.Contains(
                   CountryScopeMapper.ToScopeId(evaluation.Patient.CountryCode));
    }

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
