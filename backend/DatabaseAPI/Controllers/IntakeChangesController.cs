using System.Text.Json;
using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers;

/// <summary>
/// Staff review queue for master-data changes submitted through the public
/// intake link. Approving applies the proposed values to the patient card;
/// rejecting keeps the current card untouched.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:int}/intake-changes")]
public class IntakeChangesController : ControllerBase
{
    private readonly DatabaseContext _context;
    private readonly PatientMasterDataService _patientMasterDataService;
    private readonly IActorCountryContextService _actorCountryContextService;
    private readonly ILogger<IntakeChangesController> _logger;

    public IntakeChangesController(
        DatabaseContext context,
        PatientMasterDataService patientMasterDataService,
        IActorCountryContextService actorCountryContextService,
        ILogger<IntakeChangesController> logger)
    {
        _context = context;
        _patientMasterDataService = patientMasterDataService;
        _actorCountryContextService = actorCountryContextService;
        _logger = logger;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<List<IntakeChangeRequestDto>>> GetPending(int patientId)
    {
        var exists = await _context.Patients.AnyAsync(patient => patient.Id == patientId);
        if (!exists)
        {
            return NotFound("Patient not found");
        }

        var rows = await _context.IntakeChangeRequests
            .AsNoTracking()
            .Where(change => change.PatientId == patientId &&
                             change.Status == IntakeChangeStatuses.Pending)
            .OrderByDescending(change => change.CreatedAtUtc)
            .ToListAsync();

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost("{changeId:int}/approve")]
    public async Task<ActionResult<IntakeChangeRequestDto>> Approve(int patientId, int changeId)
    {
        var change = await _context.IntakeChangeRequests
            .FirstOrDefaultAsync(entry => entry.Id == changeId && entry.PatientId == patientId);
        if (change == null)
        {
            return NotFound("Intake change request not found");
        }
        if (change.Status != IntakeChangeStatuses.Pending)
        {
            return Conflict("Intake change request has already been reviewed");
        }

        var patient = await _context.Patients
            .Include(entry => entry.Person)
                .ThenInclude(person => person.Address)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.FirstNameHistories)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.LastNameHistories)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.EmailHistories)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.PhoneNumberHistories)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.Emails)
            .Include(entry => entry.Person)
                .ThenInclude(person => person.ContactToObjects)
                    .ThenInclude(link => link.Contact)
                        .ThenInclude(contact => contact.PhoneNumbers)
            .FirstOrDefaultAsync(entry => entry.Id == patientId);

        if (patient == null)
        {
            return NotFound("Patient not found");
        }

        CreateIntakeFormEventRequest request;
        try
        {
            request = JsonSerializer.Deserialize<CreateIntakeFormEventRequest>(change.ChangesJson)
                      ?? throw new JsonException("Empty change payload");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid stored intake change payload for change {ChangeId}", changeId);
            return BadRequest("Stored intake change payload is invalid");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _patientMasterDataService.UpdatePatientDataAsync(patient, request);

            change.Status = IntakeChangeStatuses.Approved;
            change.ReviewedAtUtc = DateTime.UtcNow;
            change.ReviewedByEmployeeId = _actorCountryContextService.GetActorEmployeeId();

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(ToDto(change));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Could not approve intake change {ChangeId}", changeId);
            return StatusCode(500, "Could not apply the intake change");
        }
    }

    [HttpPost("{changeId:int}/reject")]
    public async Task<ActionResult<IntakeChangeRequestDto>> Reject(int patientId, int changeId)
    {
        var change = await _context.IntakeChangeRequests
            .FirstOrDefaultAsync(entry => entry.Id == changeId && entry.PatientId == patientId);
        if (change == null)
        {
            return NotFound("Intake change request not found");
        }
        if (change.Status != IntakeChangeStatuses.Pending)
        {
            return Conflict("Intake change request has already been reviewed");
        }

        change.Status = IntakeChangeStatuses.Rejected;
        change.ReviewedAtUtc = DateTime.UtcNow;
        change.ReviewedByEmployeeId = _actorCountryContextService.GetActorEmployeeId();
        await _context.SaveChangesAsync();

        return Ok(ToDto(change));
    }

    private static IntakeChangeRequestDto ToDto(IntakeChangeRequest change)
    {
        MasterDataChangesDto changes = new();
        try
        {
            var request = JsonSerializer.Deserialize<CreateIntakeFormEventRequest>(change.ChangesJson);
            if (request != null)
            {
                changes = new MasterDataChangesDto
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    DateOfBirth = request.DateOfBirth,
                    Gender = request.Gender,
                    InsuranceNumber = request.InsuranceNumber,
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    Address = request.Address,
                    PostalCode = request.PostalCode
                };
            }
        }
        catch (JsonException)
        {
            // Keep an empty change view; the summary still explains the request.
        }

        return new IntakeChangeRequestDto
        {
            Id = change.Id,
            PatientId = change.PatientId,
            IntakeEventId = change.IntakeEventId,
            Status = change.Status,
            Summary = change.Summary,
            Changes = changes,
            CreatedAtUtc = change.CreatedAtUtc,
            ReviewedAtUtc = change.ReviewedAtUtc,
            ReviewedByEmployeeId = change.ReviewedByEmployeeId
        };
    }
}
