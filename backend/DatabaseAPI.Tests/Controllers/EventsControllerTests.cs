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
    private readonly IntakeFormLinkService _linkService;
    private readonly Mock<IVisitService> _visitServiceMock = new();
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

        _linkService = new IntakeFormLinkService(_context);
        SeedGdprTemplate();

        _controller = new EventsController(
            _context,
            actorCountryMock.Object,
            uidMock.Object,
            new EventService(_context),
            new EventOptionsService(_context),
            _linkService,
            _visitServiceMock.Object,
            new PatientMasterDataService(_context),
            new PatientService(_context, uidMock.Object, actorCountryMock.Object),
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

    private ConsentTemplate SeedGdprTemplate(
        string version = "1", string language = "cs", bool active = true)
    {
        var template = new ConsentTemplate
        {
            ConsentType = "gdpr",
            Version = version,
            Language = language,
            Title = "GDPR",
            Text = "GDPR text",
            IsActive = active
        };
        _context.ConsentTemplates.Add(template);
        _context.SaveChanges();
        return template;
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
            PatientId = patient.Id,
            GdprAccepted = true
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
            Smoking = true,
            GdprAccepted = true
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
            GdprAccepted = true,
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
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            GdprAccepted = true
        };

        var first = await _controller.SubmitIntakeFormByLink(token, request);
        Assert.Null(first.Result);
        Assert.NotNull(first.Value);

        var second = await _controller.SubmitIntakeFormByLink(token, request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(second.Result);
        Assert.Contains("already been used", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithGdprAccepted_RecordsConsentVersion()
    {
        var person = SeedPerson();
        SeedEventType();
        _context.ConsentTemplates.Add(new ConsentTemplate
        {
            ConsentType = "gdpr",
            Version = "2",
            Language = "cs",
            Title = "Souhlas",
            Text = "Text",
            IsActive = true
        });
        await _context.SaveChangesAsync();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ConfirmAccuracy = true,
            GdprAccepted = true,
            TermsAccepted = true
        });

        Assert.Null(result.Result);
        var consent = await _context.PatientConsents.SingleAsync();
        var patient = await _context.Patients.SingleAsync(p => p.PersonId == person.Id);
        Assert.Equal(patient.Id, consent.PatientId);
        Assert.Equal("gdpr", consent.ConsentType);
        Assert.Equal("2", consent.Version);
        Assert.Equal("IntakeForm", consent.Source);
        Assert.NotNull(consent.IntakeEventId);
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithChangedMasterData_QueuesApproval()
    {
        var person = SeedPerson(firstName: "Eva", lastName: "Kralova");
        SeedEventType();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            FirstName = "Evicka",
            Email = "new@example.com",
            GdprAccepted = true
        });

        Assert.Null(result.Result);
        var change = await _context.IntakeChangeRequests.SingleAsync();
        Assert.Equal(IntakeChangeStatuses.Pending, change.Status);
        Assert.Contains("Evicka", change.ChangesJson);

        var unchanged = await _context.Persons.SingleAsync(entry => entry.Id == person.Id);
        Assert.Equal("Eva", unchanged.FirstName);
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithoutGdprConsent_ReturnsBadRequest()
    {
        var person = SeedPerson();
        SeedEventType();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ConfirmAccuracy = true,
            TermsAccepted = true
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("GDPR consent is required", badRequest.Value?.ToString());
        Assert.Empty(await _context.FormSubmissions.ToListAsync());
        Assert.Empty(await _context.Patients.ToListAsync());
        Assert.Null((await _context.IntakeFormLinks.SingleAsync()).UsedAtUtc);
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithoutActiveGdprTemplate_ReturnsBadRequest()
    {
        var person = SeedPerson();
        SeedEventType();
        foreach (var template in _context.ConsentTemplates)
        {
            template.IsActive = false;
        }
        await _context.SaveChangesAsync();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            GdprAccepted = true
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("not available", badRequest.Value?.ToString());
        Assert.Empty(await _context.Patients.ToListAsync());
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_WithStaleGdprVersion_ReturnsBadRequest()
    {
        var person = SeedPerson();
        SeedEventType();
        var (token, _) = await CreateValidLink(person);

        var result = await _controller.SubmitIntakeFormByLink(token, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            GdprAccepted = true,
            GdprLanguage = "cs",
            GdprVersion = "0"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("version '0'", badRequest.Value?.ToString());
        Assert.Empty(await _context.Patients.ToListAsync());
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
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            GdprAccepted = true
        });

        Assert.Null(result.Result);
        Assert.NotNull(result.Value);
        var updated = await _context.Persons.SingleAsync(p => p.Id == person.Id);
        Assert.Equal("00000099", updated.UID); // from the mocked IUidService
    }

    private ReservationSlot SeedReservationSlot(int personId, DateTime start)
    {
        var slot = new ReservationSlot
        {
            HospitalId = 1,
            PersonId = personId,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            Status = SlotStatus.Reserved,
            CountryCode = "CZ"
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    [Fact]
    public async Task SubmitIntakeFormByLink_ForReservationSlot_CanBeEditedUntilVisit()
    {
        var person = SeedPerson();
        SeedEventType();
        var slot = SeedReservationSlot(person.Id, DateTime.UtcNow.AddDays(2));
        var created = await _linkService.CreateLinkForReservationSlotAsync(
            person.Id, slot.Id, slot.StartDateTime);
        Assert.NotNull(created);
        var token = created!.Value.RawToken;

        var request = new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1990, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ConfirmAccuracy = true,
            GdprAccepted = true,
            TermsAccepted = true
        };

        var first = await _controller.SubmitIntakeFormByLink(token, request);
        Assert.Null(first.Result);

        var afterFirst = await _controller.GetIntakeFormLinkInfo(token);
        var firstInfo = Assert.IsType<IntakeFormLinkInfoDto>(
            Assert.IsType<OkObjectResult>(afterFirst.Result).Value);
        Assert.True(firstInfo.CanEdit);
        Assert.True(firstInfo.AlreadySubmitted);
        Assert.Equal(slot.StartDateTime, firstInfo.VisitStartUtc);

        var second = await _controller.SubmitIntakeFormByLink(token, request);
        Assert.Null(second.Result);

        var link = await _context.IntakeFormLinks.SingleAsync(l => l.Id == created.Value.Link.Id);
        Assert.NotNull(link.UsedAtUtc);
        Assert.NotNull(link.LastSubmittedAtUtc);

        _visitServiceMock.Verify(
            service => service.LinkIntakeEventAsync(
                person.Id, null, slot.Id, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetIntakeFormLinkInfo_PrefillsDemographicsAndLatestMedicalData()
    {
        var person = SeedPerson();
        SeedEventType();
        var (firstToken, _) = await CreateValidLink(person);

        var submitted = await _controller.SubmitIntakeFormByLink(firstToken, new CreateIntakeFormEventRequest
        {
            DateOfBirth = new DateTime(1985, 4, 2, 0, 0, 0, DateTimeKind.Utc),
            InsuranceNumber = 55,
            Email = "eva@example.com",
            PhoneNumber = "777123456",
            Weight = 72,
            Height = 170,
            MedDiabetes = true,
            Smoking = true,
            LastMealHours = "6",
            HadCovid = true,
            CovidWhen = "2021",
            CovidVaccine = true,
            AdditionalHealthInfo = "note",
            ConfirmAccuracy = true,
            GdprAccepted = true,
            TermsAccepted = true
        });
        Assert.Null(submitted.Result);

        var (secondToken, _) = await CreateValidLink(person);
        var infoResult = await _controller.GetIntakeFormLinkInfo(secondToken);
        var info = Assert.IsType<IntakeFormLinkInfoDto>(
            Assert.IsType<OkObjectResult>(infoResult.Result).Value);

        Assert.True(info.GdprAvailable);
        Assert.Equal("1", info.GdprVersion);
        Assert.Equal("Eva", info.Prefill.FirstName);
        Assert.Equal(new DateTime(1985, 4, 2), info.Prefill.DateOfBirth);
        Assert.Equal(55, info.Prefill.InsuranceNumber);
        Assert.Equal("eva@example.com", info.Prefill.Email);
        Assert.Equal("777123456", info.Prefill.PhoneNumber);
        Assert.Equal(72, info.Prefill.Weight);
        Assert.Equal(170, info.Prefill.Height);
        Assert.True(info.Prefill.MedDiabetes);
        Assert.True(info.Prefill.Smoking);
        Assert.Equal("6", info.Prefill.LastMealHours);
        Assert.True(info.Prefill.HadCovid);
        Assert.Equal("2021", info.Prefill.CovidWhen);
        Assert.True(info.Prefill.CovidVaccine);
        Assert.Equal("note", info.Prefill.AdditionalHealthInfo);
    }

    [Fact]
    public async Task CreatePatientWithIntake_RejectsAnIdenticalLivePatient()
    {
        // Simulates a retried "add patient" submission: the first request may
        // have committed even though the browser showed an error.
        var person = SeedPerson("Jana", "Novakova", "00000077");
        var existing = SeedPatient(person);
        var patientCount = _context.Patients.Count();

        var result = await _controller.CreatePatientWithIntake(new CreatePatientWithIntakeRequest
        {
            Patient = new CreatePatientRequest
            {
                FirstName = "Jana",
                LastName = "Novakova",
                BirthDate = new DateTime(1980, 1, 1),
                Gender = "F"
            },
            Intake = new CreateIntakeFormEventRequest
            {
                ConfirmAccuracy = true,
                GdprAccepted = true,
                TermsAccepted = true
            }
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        var existingId = conflict.Value!
            .GetType()
            .GetProperty("existingPatientId")!
            .GetValue(conflict.Value);
        Assert.Equal(existing.Id, existingId);
        Assert.Equal(patientCount, _context.Patients.Count());
    }
}
