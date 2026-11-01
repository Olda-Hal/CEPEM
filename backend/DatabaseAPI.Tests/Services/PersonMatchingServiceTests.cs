using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Public reservation matching: only an exact e-mail AND phone pair selects an
/// existing person; ambiguity always falls back to a new record.
/// </summary>
public class PersonMatchingServiceTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly PersonMatchingService _service;

    public PersonMatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _service = new PersonMatchingService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Person SeedPerson(
        string firstName,
        string email,
        string phone,
        bool employee = false,
        bool patient = true,
        bool deleted = false)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = "Test",
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
        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = phone });

        if (patient)
        {
            _context.Patients.Add(new Patient
            {
                PersonId = person.Id,
                BirthDate = new DateTime(1990, 1, 1),
                CountryCode = "CZ",
                IsDeleted = deleted
            });
        }

        if (employee)
        {
            _context.Employees.Add(new Employee
            {
                PersonId = person.Id,
                PasswordHash = "hash",
                Salt = "salt"
            });
        }

        _context.SaveChanges();
        return person;
    }

    [Fact]
    public async Task MatchingEmailAndPhone_ReturnsExistingPerson()
    {
        var person = SeedPerson("Eva", "Eva@Example.com", "777 123 456");

        var match = await _service.FindUniquePersonByContactsAsync("  eva@example.com ", "777123456");

        Assert.NotNull(match);
        Assert.Equal(person.Id, match!.Id);
    }

    [Fact]
    public async Task EmailWithoutPhone_DoesNotMatch()
    {
        SeedPerson("Eva", "eva@example.com", "777123456");

        Assert.Null(await _service.FindUniquePersonByContactsAsync("eva@example.com", null));
        Assert.Null(await _service.FindUniquePersonByContactsAsync("eva@example.com", "111222333"));
    }

    [Fact]
    public async Task PhoneWithoutEmail_DoesNotMatch()
    {
        SeedPerson("Eva", "eva@example.com", "777123456");

        Assert.Null(await _service.FindUniquePersonByContactsAsync(null, "777123456"));
        Assert.Null(await _service.FindUniquePersonByContactsAsync("other@example.com", "777123456"));
    }

    [Fact]
    public async Task AmbiguousContacts_DoNotMatch()
    {
        SeedPerson("Eva", "shared@example.com", "777123456");
        SeedPerson("Eva", "shared@example.com", "777 123 456");

        Assert.Null(await _service.FindUniquePersonByContactsAsync("shared@example.com", "777123456"));
    }

    [Fact]
    public async Task EmployeeIsNeverMatched()
    {
        SeedPerson("Staff", "staff@example.com", "777123456", employee: true, patient: false);

        Assert.Null(await _service.FindUniquePersonByContactsAsync("staff@example.com", "777123456"));
    }

    [Fact]
    public async Task DeletedPatientIsNeverMatched()
    {
        SeedPerson("Gone", "gone@example.com", "777123456", deleted: true);

        Assert.Null(await _service.FindUniquePersonByContactsAsync("gone@example.com", "777123456"));
    }
}
