using DatabaseAPI.APIModels;
using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>
/// Coverage for event options, option creation, event/event-group creation
/// (via the real EventService) and the risky intake-form magic-link flow:
/// unknown/revoked/used/expired tokens, single-use semantics and the
/// person -&gt; patient promotion on submit.
/// </summary>
public class EventsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly EventsController _controller;

    public EventsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new DatabaseContext(options);

        var actorCountryMock = new Mock<IActorCountryContextService>();
        actorCountryMock.Setup(service => service.GetActorCountryCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZ");

        var uidMock = new Mock<IUidService>();
        uidMock.Setup(service => service.GetNextAvailableUidAsync()).ReturnsAsync("00000099");

        _controller = new EventsController(
            _context,
            actorCountryMock.Object,
            uidMock.Object,
            new EventService(_context),
            new EventOptionsService(_context),
            new IntakeFormLinkService(_context),
            NullLogger<EventsController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private EventType SeedEventType(string en = "Intake Form", string? cs = "Vstupní Formulář")
    {
        var type = new EventType { NameTranslation = new Translation { EN = en, CS = cs } };
        _context.EventTypes.Add(type);
        _context.SaveChanges();
        return type;
    }

    private Person SeedPerson(string firstName = "Eva", string lastName = "Kralova", string uid = "00000020")
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
            UID = uid,
            Gender = "F",
            CountryCode = "CZ",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();
        return person;
    }

    private Patient SeedPatient(Person? person = null)
    {
        person ??= SeedPerson();
        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            InsuranceNumber = 111,
            Alive = true,
            CountryCode = "CZ"
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    // ---- options ----

    [Fact]
    public async Task GetEventOptions_DefaultsToCzech_WithEnglishFallback()
    {
        _context.EventTypes.Add(new EventType { NameTranslation = new Translation { EN = "Checkup", CS = "Prohlídka" } });
        _context.EventTypes.Add(new EventType { NameTranslation = new Translation { EN = "Only English" } });
        await _context.SaveChangesAsync();

        var result = await _controller.GetEventOptions();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var options = Assert.IsType<EventOptionsResponse>(ok.Value);
        Assert.Contains(options.EventTypes, t => t.Name == "Prohlídka");
        Assert.Contains(options.EventTypes, t => t.Name == "Only English");
    }

    [Fact]
    public async Task GetEventOptions_WithEnglishLanguage_ReturnsEnglishNames()
    {
        _context.EventTypes.Add(new EventType { NameTranslation = new Translation { EN = "Checkup", CS = "Prohlídka" } });
        await _context.SaveChangesAsync();

        var result = await _controller.GetEventOptions("en");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var options = Assert.IsType<EventOptionsResponse>(ok.Value);
        Assert.Contains(options.EventTypes, t => t.Name == "Checkup");
    }

    // ---- event creation ----

    [Fact]
    public async Task CreateEvent_PersistsEventWithCommentAndExaminations()
    {
        var patient = SeedPatient();
        SeedEventType("Preventive examination", null);
        var examinationType = new ExaminationType { NameTranslation = new Translation { EN = "Blood draw" } };
        _context.ExaminationTypes.Add(examinationType);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateEvent(new CreateEventRequest
        {
            PatientId = patient.Id,
            EventTypeId = _context.EventTypes.Single().Id,
            HappenedAt = DateTime.UtcNow,
            Comment = "ok",
            ExaminationTypeIds = new List<int> { examinationType.Id }
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var created = Assert.IsType<CreateEventResponse>(ok.Value);
        Assert.True(created.EventId > 0);
        Assert.Single(created.ExaminationIds);

        var persistedEvent = await _context.Events
            .Include(e => e.Comment)
            .SingleAsync(e => e.Id == created.EventId);
        Assert.Equal("ok", persistedEvent.Comment!.Text);
        Assert.Equal("CZ", persistedEvent.CountryCode);
    }

    [Fact]
    public async Task CreateEventGroup_SharesOneGroupIdAcrossEvents()
    {
        var patient = SeedPatient();
        var eventType = SeedEventType("Preventive examination", null);

        var result = await _controller.CreateEventGroup(new CreateEventGroupRequest
        {
            PatientId = patient.Id,
            Events = new List<CreateEventRequest>
            {
                new() { PatientId = 0, EventTypeId = eventType.Id, HappenedAt = DateTime.UtcNow },
                new() { PatientId = 0, EventTypeId = eventType.Id, HappenedAt = DateTime.UtcNow }
            }
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var group = Assert.IsType<CreateEventGroupResponse>(ok.Value);
        Assert.Equal(2, group.EventIds.Count);
        var groupIds = await _context.Events
            .Where(e => group.EventIds.Contains(e.Id))
            .Select(e => e.EventGroupId)
            .Distinct()
            .ToListAsync();
        Assert.Single(groupIds);
        Assert.Equal(group.EventGroupId, groupIds[0]);
    }

    [Fact]
    public async Task CreateOptionEndpoint_AddsTranslationAndEntity()
    {
        var result = await _controller.CreateSymptom(new CreateOptionRequest { Name = "Bolest hlavy" });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var created = Assert.IsType<SymptomResponse>(ok.Value);
        Assert.Equal("Bolest hlavy", created.Name);
        Assert.True(await _context.Symptoms.AnyAsync());
        Assert.True(await _context.Translations.AnyAsync(t => t.EN == "Bolest hlavy"));
    }

    // ---- intake form event ----

    [Fact]
    public async Task CreateIntakeFormEvent_ForUnknownPatient_ReturnsNotFound()
    {
        var result = await _controller.CreateIntakeFormEvent(new CreateIntakeFormEventRequest
        {
            PatientId = 99999
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateIntakeFormEvent_WithoutIntakeEventType_ReturnsBadRequest()
    {
        var patient = SeedPatient();

        var result = await _controller.CreateIntakeFormEvent(new CreateIntakeFormEventRequest
        {
            PatientId = patient.Id
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Intake Form event type not found", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task CreateIntakeFormEvent_UpdatesPatientAndStoresSubmission()
    {
        var patient = SeedPatient();
        SeedEventType();
        var contact = new Contact();
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = "old@example.com" });
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = patient.PersonId,
            ObjectType = ContactObjectType.Person,
            PersonId = patient.PersonId
        });
        await _context.SaveChangesAsync();

        var result = await _controller.CreateIntakeFormEvent(new CreateIntakeFormEventRequest
        {
            PatientId = patient.Id,
            FirstName = "Eva",
            LastName = "Kralova-Nova",
            Email = "new@example.com",
            Weight = 65,
            Height = 170,
            MedDiabetes = true,
            Smoking = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<IntakeFormEventDto>(ok.Value);
        Assert.Contains("Léky: Cukrovka", dto.Comment);
        Assert.Contains("Kouření", dto.Comment);
        Assert.Contains("Příjmení:", dto.Comment, StringComparison.Ordinal); // name change captured

        var person = await _context.Persons.SingleAsync(p => p.Id == patient.PersonId);
        Assert.Equal("Kralova-Nova", person.LastName);

        Assert.True(await _context.FormSubmissions.AnyAsync(s => s.EventId == dto.EventId));
        Assert.True(await _context.FormSubmissionMedications.AnyAsync(m => m.MedDiabetes));
        Assert.True(await _context.LastNameHistories.AnyAsync(h => h.LastName == "Kralova"));
    }

    // ---- intake form links (magic links) ----

    [Fact]
    public async Task CreateIntakeFormLink_ForUnknownPerson_ReturnsNotFound()
    {
        var result = await _controller.CreateIntakeFormLink(new CreateIntakeFormLinkRequest
        {
            PersonId = 99999
        });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateIntakeFormLink_ReturnsToken_AndStoresOnlyHash()
    {
        var person = SeedPerson();

        var result = await _controller.CreateIntakeFormLink(new CreateIntakeFormLinkRequest
        {
            PersonId = person.Id,
            ExpiresInHours = 24
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<IntakeFormLinkDto>(ok.Value);
        Assert.Equal(64, dto.Token.Length);
        Assert.Equal($"intake/{dto.Token}", dto.IntakePath.TrimStart('/'));

        var stored = await _context.IntakeFormLinks.SingleAsync(l => l.Id == dto.LinkId);
        Assert.NotEqual(dto.Token, stored.TokenHash);
        Assert.DoesNotContain(dto.Token, stored.TokenHash, StringComparison.OrdinalIgnoreCase);
        Assert.True(stored.ExpiresAtUtc > DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task CreateIntakeFormLink_WithWrongReservation_ReturnsBadRequest()
    {
        var person = SeedPerson(uid: "00000021");
        var otherPerson = SeedPerson("Karel", "Hrbek", "00000022");
        var reservation = new Reservation
        {
            ExaminationRoomId = 1,
            DoctorId = 1,
            PersonId = otherPerson.Id,
            ExaminationTypeId = 1,
            StartDateTime = DateTime.UtcNow,
            EndDateTime = DateTime.UtcNow.AddHours(1)
        };
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        var result = await _controller.CreateIntakeFormLink(new CreateIntakeFormLinkRequest
        {
            PersonId = person.Id,
            ReservationId = reservation.Id
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private async Task<(string token, int linkId)> CreateValidLink(Person person)
    {
        var result = await _controller.CreateIntakeFormLink(new CreateIntakeFormLinkRequest
        {
            PersonId = person.Id,
            ExpiresInHours = 72
        });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<IntakeFormLinkDto>(ok.Value);
        return (dto.Token, dto.LinkId);
    }

    [Fact]
    public async Task GetIntakeFormLinkInfo_WithValidToken_ReturnsPersonInfo()
    {
        var person = SeedPerson();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.GetIntakeFormLinkInfo(token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<IntakeFormLinkInfoDto>(ok.Value);
        Assert.Equal(person.Id, info.PersonId);
        Assert.Equal("Eva", info.FirstName);
    }

    [Fact]
    public async Task GetIntakeFormLinkInfo_WithUnknownToken_ReturnsNotFound()
    {
        var result = await _controller.GetIntakeFormLinkInfo("deadbeef");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetIntakeFormLinkInfo_WithExpiredToken_ReturnsBadRequest()
    {
        var person = SeedPerson();
        var (token, linkId) = await CreateValidLink(person);
        var link = await _context.IntakeFormLinks.SingleAsync(l => l.Id == linkId);
        link.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        await _context.SaveChangesAsync();

        var result = await _controller.GetIntakeFormLinkInfo(token);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("expired", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task GetIntakeFormLinkInfo_WithRevokedToken_ReturnsBadRequest()
    {
        var person = SeedPerson();
        var (token, linkId) = await CreateValidLink(person);
        var link = await _context.IntakeFormLinks.SingleAsync(l => l.Id == linkId);
        link.RevokedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var result = await _controller.GetIntakeFormLinkInfo(token);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("revoked", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithValidToken_CreatesPatientAndMarksLinkUsed()
    {
        var person = SeedPerson();
        SeedEventType();
        var (token, linkId) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            InsuranceNumber = 55,
            Weight = 70,
            ConfirmAccuracy = true,
            TermsAccepted = true
        });

        Assert.Null(result.Result); // returns the DTO directly, not Ok(...)
        Assert.NotNull(result.Value);

        var patient = await _context.Patients.SingleAsync(p => p.PersonId == person.Id);
        Assert.Equal(new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc), patient.BirthDate);
        Assert.Equal(55, patient.InsuranceNumber);

        var link = await _context.IntakeFormLinks.SingleAsync(l => l.Id == linkId);
        Assert.NotNull(link.UsedAtUtc);
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_CannotBeUsedTwice()
    {
        var person = SeedPerson();
        SeedEventType();
        var (token, _) = await CreateValidLink(person);

        var request = new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var first = await _controller.SubmitIntakeFormByLink(token, request);
        Assert.Null(first.Result);
        Assert.NotNull(first.Value);

        var second = await _controller.SubmitIntakeFormByLink(token, request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(second.Result);
        Assert.Contains("already been used", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithUnknownToken_ReturnsNotFound()
    {
        var result = await _controller.SubmitIntakeFormByLink("deadbeef", new CreateIntakeFormEventRequest());

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithNonNumericUid_PromotesPersonWithFreshUid()
    {
        var person = SeedPerson(uid: "res-abc123");
        SeedEventType();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        Assert.Null(result.Result);
        Assert.NotNull(result.Value);
        var updated = await _context.Persons.SingleAsync(p => p.Id == person.Id);
        Assert.Equal("00000099", updated.UID); // from the mocked IUidService
    }
}
