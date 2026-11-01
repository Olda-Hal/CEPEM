using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Intake reminder run: editable template path, URL rendering, fallback e-mail
/// and claim release when sending fails.
/// </summary>
public class IntakeReminderServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IIntakeFormLinkService> _linkMock = new();
    private readonly Mock<IEmailService> _emailMock = new();
    private readonly Mock<ICommunicationTemplateService> _templateMock = new();

    public IntakeReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Person SeedPersonWithEmail(string email = "eva@example.com")
    {
        var person = new Person
        {
            FirstName = "Eva",
            LastName = "Kralova",
            UID = Guid.NewGuid().ToString("N")[..10],
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var contact = new Contact();
        _context.Contacts.Add(contact);
        _context.SaveChanges();
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = person.Id,
            ObjectType = ContactObjectType.Person,
            PersonId = person.Id
        });
        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = email });
        _context.SaveChanges();
        return person;
    }

    private ReservationSlot SeedSlot(DateTime start, Person person, DateTime? remindedAt = null)
    {
        var slot = new ReservationSlot
        {
            HospitalId = 1,
            PersonId = person.Id,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            Status = SlotStatus.Reserved,
            CountryCode = "CZ",
            IntakeReminderSentAtUtc = remindedAt
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    private IntakeReminderService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:FrontendBaseUrl"] = "https://app.example/"
            })
            .Build();

        return new IntakeReminderService(
            new Mock<IServiceScopeFactory>().Object,
            _emailMock.Object,
            configuration,
            NullLogger<IntakeReminderService>.Instance);
    }

    private void SeedLink()
    {
        _linkMock
            .Setup(service => service.CreateLinkForReservationSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new IntakeFormLink(), "raw-token"));
    }

    [Fact]
    public async Task RunOnce_SendsTemplatedEmailAndMarksSlot()
    {
        var person = SeedPersonWithEmail();
        var slot = SeedSlot(DateTime.UtcNow.AddDays(2), person);
        SeedLink();
        _templateMock
            .Setup(service => service.ResolveAsync(
                CommunicationTemplateCodes.IntakeReminder,
                CommunicationChannels.Email,
                "cs",
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationTemplate
            {
                Code = CommunicationTemplateCodes.IntakeReminder,
                Channel = CommunicationChannels.Email,
                Language = "cs",
                Subject = "Vyplnte formular",
                Body = "Odkaz {{link}}"
            });
        _templateMock
            .Setup(service => service.Render(
                It.IsAny<string>(),
                It.Is<IReadOnlyDictionary<string, string?>>(values =>
                    values["link"] == "https://app.example/intake/raw-token")))
            .Returns("Rendered intake");

        var sent = await CreateService().RunOnceAsync(
            _context, _linkMock.Object, _templateMock.Object, leadDays: 3);

        Assert.Equal(1, sent);
        _emailMock.Verify(service => service.SendTemplatedAsync(
            "eva@example.com",
            "Vyplnte formular",
            "Rendered intake",
            EmailLogKinds.IntakeReminder,
            It.Is<EmailLogContext>(context =>
                context.ReservationSlotId == slot.Id && context.PersonId == person.Id)),
            Times.Once);
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.NotNull(stored.IntakeReminderSentAtUtc);
    }

    [Fact]
    public async Task RunOnce_WithoutTemplate_UsesFallbackEmail()
    {
        var person = SeedPersonWithEmail();
        SeedSlot(DateTime.UtcNow.AddDays(2), person);
        SeedLink();
        _templateMock
            .Setup(service => service.ResolveAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommunicationTemplate?)null);

        var sent = await CreateService().RunOnceAsync(
            _context, _linkMock.Object, _templateMock.Object, leadDays: 3);

        Assert.Equal(1, sent);
        _emailMock.Verify(service => service.SendIntakeFormReminderAsync(
            "eva@example.com",
            "https://app.example/intake/raw-token",
            "cs",
            It.IsAny<DateTime>(),
            It.IsAny<EmailLogContext>()), Times.Once);
        _emailMock.Verify(service => service.SendTemplatedAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<EmailLogContext>()), Times.Never);
    }

    [Fact]
    public async Task RunOnce_SendFailure_ReleasesClaim()
    {
        var person = SeedPersonWithEmail();
        var slot = SeedSlot(DateTime.UtcNow.AddDays(2), person);
        SeedLink();
        _templateMock
            .Setup(service => service.ResolveAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommunicationTemplate
            {
                Code = CommunicationTemplateCodes.IntakeReminder,
                Channel = CommunicationChannels.Email,
                Language = "cs",
                Subject = "Subject",
                Body = "Body"
            });
        _templateMock
            .Setup(service => service.Render(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns("Body");
        _emailMock
            .Setup(service => service.SendTemplatedAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<EmailLogContext>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var sent = await CreateService().RunOnceAsync(
            _context, _linkMock.Object, _templateMock.Object, leadDays: 3);

        Assert.Equal(0, sent);
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.Null(stored.IntakeReminderSentAtUtc);
    }

    [Fact]
    public async Task RunOnce_WithoutEmail_SkipsAndDoesNotClaimSlot()
    {
        var person = new Person
        {
            FirstName = "Jan",
            LastName = "Novak",
            UID = Guid.NewGuid().ToString("N")[..10],
            Gender = "M",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();
        var slot = SeedSlot(DateTime.UtcNow.AddDays(2), person);
        SeedLink();

        var sent = await CreateService().RunOnceAsync(
            _context, _linkMock.Object, _templateMock.Object, leadDays: 3);

        Assert.Equal(0, sent);
        _linkMock.Verify(service => service.CreateLinkForReservationSlotAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime>()), Times.Never);
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.Null(stored.IntakeReminderSentAtUtc);
    }
}
