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
/// Day-before SMS reminder: only confirmed slots inside the lead window,
/// with a phone number and an active template, sent at most once.
/// </summary>
public class SmsReminderServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly CommunicationTemplateService _templates;

    public SmsReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _templates = new CommunicationTemplateService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Person SeedPersonWithPhone(string phone = "+420777123456")
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

        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber
        {
            ContactId = contact.Id,
            PhoneNumber = phone
        });
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = person.Id,
            ObjectType = ContactObjectType.Person,
            PersonId = person.Id
        });
        _context.SaveChanges();
        return person;
    }

    private ReservationSlot SeedSlot(DateTime start, Person person, DateTime? sentAt = null)
    {
        var slot = new ReservationSlot
        {
            HospitalId = 1,
            PersonId = person.Id,
            StartDateTime = start,
            EndDateTime = start.AddHours(1),
            Status = SlotStatus.Reserved,
            CountryCode = "CZ",
            SmsReminderSentAtUtc = sentAt
        };
        _context.ReservationSlots.Add(slot);
        _context.SaveChanges();
        return slot;
    }

    private void SeedTemplate(
        string language = "cs",
        string body = "Pripominka {{date}} v {{time}}",
        string scopeType = CommunicationScopes.Global,
        int scopeId = 0)
    {
        _context.CommunicationTemplates.Add(new CommunicationTemplate
        {
            Code = CommunicationTemplateCodes.AppointmentReminderSms,
            Channel = CommunicationChannels.Sms,
            Language = language,
            ScopeType = scopeType,
            ScopeId = scopeId,
            Body = body,
            IsActive = true
        });
        _context.SaveChanges();
    }

    private SmsReminderService CreateService(ISmsService smsService) =>
        new(
            new Mock<IServiceScopeFactory>().Object,
            smsService,
            new ConfigurationBuilder().Build(),
            NullLogger<SmsReminderService>.Instance);

    [Fact]
    public async Task RunOnce_SendsTemplateAndMarksSlot()
    {
        var person = SeedPersonWithPhone();
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        SeedTemplate();

        var sms = new Mock<ISmsService>();
        sms.Setup(service => service.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SmsLogContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(sms.Object);
        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(1, sent);
        sms.Verify(service => service.SendAsync(
            "+420777123456",
            It.Is<string>(body => body.Contains("Pripominka") && !body.Contains("{{")),
            CommunicationTemplateCodes.AppointmentReminderSms,
            It.IsAny<SmsLogContext>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.NotNull(stored.SmsReminderSentAtUtc);
    }

    [Fact]
    public async Task RunOnce_DoesNotSendTwice()
    {
        var person = SeedPersonWithPhone();
        SeedSlot(DateTime.UtcNow.AddHours(12), person, sentAt: DateTime.UtcNow.AddMinutes(-5));
        SeedTemplate();

        var sms = new Mock<ISmsService>();
        var service = CreateService(sms.Object);

        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(0, sent);
        sms.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnce_WithoutTemplate_SkipsAndDoesNotClaimSlot()
    {
        var person = SeedPersonWithPhone();
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);

        var sms = new Mock<ISmsService>();
        var service = CreateService(sms.Object);

        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(0, sent);
        sms.VerifyNoOtherCalls();
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.Null(stored.SmsReminderSentAtUtc);
    }

    [Fact]
    public async Task RunOnce_SendFailure_ReleasesClaimForRetry()
    {
        var person = SeedPersonWithPhone();
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        SeedTemplate();

        var sms = new Mock<ISmsService>();
        sms.Setup(service => service.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<SmsLogContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gateway down"));

        var service = CreateService(sms.Object);
        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(0, sent);
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.Null(stored.SmsReminderSentAtUtc);
    }

    [Fact]
    public async Task RunOnce_UsesDutchLanguageForNlSlot()
    {
        var person = SeedPersonWithPhone();
        person.CountryCode = "NL";
        await _context.SaveChangesAsync();
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        slot.CountryCode = "NL";
        await _context.SaveChangesAsync();
        SeedTemplate(language: "nl", body: "Herinnering {{date}}");

        var sms = new Mock<ISmsService>();
        var service = CreateService(sms.Object);

        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(1, sent);
        sms.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.Is<string>(body => body.StartsWith("Herinnering")),
            It.IsAny<string>(),
            It.IsAny<SmsLogContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnce_CenterTemplateOverridesGlobal()
    {
        var person = SeedPersonWithPhone();
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        SeedTemplate(body: "Global {{date}}");
        SeedTemplate(body: "Center {{date}}", scopeType: CommunicationScopes.Center, scopeId: slot.HospitalId);

        var sms = new Mock<ISmsService>();
        var service = CreateService(sms.Object);

        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(1, sent);
        sms.Verify(service => service.SendAsync(
            It.IsAny<string>(),
            It.Is<string>(body => body.StartsWith("Center")),
            It.IsAny<string>(),
            It.IsAny<SmsLogContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindDue_IgnoresSlotsWithoutPhone()
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
        var slot = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        SeedTemplate();

        var sms = new Mock<ISmsService>();
        var service = CreateService(sms.Object);

        var sent = await service.RunOnceAsync(_context, _templates, sms.Object, leadHours: 24);

        Assert.Equal(0, sent);
        var stored = await _context.ReservationSlots.SingleAsync(entry => entry.Id == slot.Id);
        Assert.Null(stored.SmsReminderSentAtUtc);
    }

    [Fact]
    public async Task Planner_ReturnsOnlyConfirmedSlotsInWindow()
    {
        var person = SeedPersonWithPhone();
        var due = SeedSlot(DateTime.UtcNow.AddHours(12), person);
        SeedSlot(DateTime.UtcNow.AddHours(48), person);
        SeedSlot(DateTime.UtcNow.AddHours(12), person, sentAt: DateTime.UtcNow);

        var found = await SmsReminderPlanner.FindDueSlotsAsync(_context, DateTime.UtcNow, leadHours: 24);

        Assert.Equal(due.Id, Assert.Single(found).Id);
    }
}
