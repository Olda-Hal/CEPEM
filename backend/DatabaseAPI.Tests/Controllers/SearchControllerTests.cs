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

/// <summary>Aggregated search: matching, category flags, deleted patients and employee scope.</summary>
public class SearchControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly Mock<IEmployeeScopeService> _scopeMock = new();
    private readonly SearchController _controller;

    public SearchControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        _scopeMock
            .Setup(service => service.GetActorScopeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActorScope(1, ["SysAdmin"], [], []));
        _scopeMock
            .Setup(service => service.GetVisibleEmployeeIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((HashSet<int>?)null);

        _controller = new SearchController(
            _context, _scopeMock.Object, NullLogger<SearchController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Patient SeedPatient(
        string firstName = "Eva",
        string lastName = "Novakova",
        string uid = "00000001",
        string email = "eva@example.com",
        string phone = "777123456",
        bool deleted = false)
    {
        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
            UID = uid,
            Gender = "F",
            CountryCode = "CZ",
            Active = true
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var contact = new Contact();
        _context.Contacts.Add(contact);
        _context.SaveChanges();
        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = email });
        _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = phone });
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = person.Id,
            ObjectType = ContactObjectType.Person,
            PersonId = person.Id
        });

        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = new DateTime(1990, 1, 1),
            CountryCode = "CZ",
            IsDeleted = deleted
        };
        _context.Patients.Add(patient);
        _context.SaveChanges();
        return patient;
    }

    private async Task<GlobalSearchResponse> SearchAsync(
        string query, bool patients = true, bool evaluations = true, bool employees = true)
    {
        var result = await _controller.Search(query, 5, patients, evaluations, employees);
        return Assert.IsType<GlobalSearchResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task Search_ByPatientName_ReturnsPatientAndVisit()
    {
        var patient = SeedPatient();
        _context.Visits.Add(new Visit
        {
            PatientId = patient.Id,
            VisitDate = new DateTime(2026, 11, 1),
            Note = "Kontrola"
        });
        _context.PatientDocuments.Add(new PatientDocument
        {
            PatientId = patient.Id,
            FileName = "encrypted-name",
            OriginalFileName = "zprava.pdf",
            UploadedAt = DateTime.UtcNow,
            FileSize = 10,
            EncryptedPath = "path"
        });
        await _context.SaveChangesAsync();

        var response = await SearchAsync("Novakova");

        Assert.Equal("Eva Novakova", $"{response.Patients.Single().FirstName} {response.Patients.Single().LastName}");
        Assert.Single(response.Visits);
        Assert.Empty(response.Documents);

        var byFile = await SearchAsync("zprava");
        Assert.Single(byFile.Documents);
        Assert.Equal(patient.Id, byFile.Documents[0].PatientId);
    }

    [Fact]
    public async Task Search_ByEmailAndPhone_ReturnsPatient()
    {
        SeedPatient(email: "hledana@example.com", phone: "123456789");

        Assert.Single((await SearchAsync("hledana@example.com")).Patients);
        Assert.Single((await SearchAsync("123456789")).Patients);
    }

    [Fact]
    public async Task Search_ExcludesDeletedPatients()
    {
        SeedPatient(deleted: true);

        var response = await SearchAsync("Novakova");

        Assert.Empty(response.Patients);
    }

    [Fact]
    public async Task Search_DocumentFullText_ReturnsSnippet()
    {
        var patient = SeedPatient();
        _context.PatientDocuments.Add(new PatientDocument
        {
            PatientId = patient.Id,
            FileName = "encrypted.pdf.enc",
            OriginalFileName = "scan.pdf",
            UploadedAt = DateTime.UtcNow,
            FileSize = 10,
            EncryptedPath = "path",
            ExtractedText = "Zprava obsahuje diabetes a hypertenzi.",
            ExtractionStatus = DocumentExtractionStatus.Extracted,
            ExtractedAtUtc = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var response = await SearchAsync("diabetes");

        var document = Assert.Single(response.Documents);
        Assert.Equal("scan.pdf", document.FileName);
        Assert.Contains("diabetes", document.Snippet);
    }

    [Fact]
    public async Task Search_RespectsCategoryFlags()
    {
        SeedPatient();

        var response = await SearchAsync("Novakova", patients: false, employees: false);

        Assert.Empty(response.Patients);
        Assert.Empty(response.Visits);
        Assert.Empty(response.Documents);
        Assert.Empty(response.Employees);
    }

    [Fact]
    public async Task Search_ShortQuery_ReturnsEmpty()
    {
        SeedPatient();

        var response = await SearchAsync("N");

        Assert.Empty(response.Patients);
        Assert.Empty(response.Visits);
        Assert.Empty(response.Evaluations);
        Assert.Empty(response.Employees);
    }

    [Fact]
    public async Task Search_Employees_RespectsVisibleScope()
    {
        var visiblePerson = new Person
        {
            FirstName = "Viditelny", LastName = "Zamestnanec", UID = "10",
            Gender = "M", CountryCode = "CZ", Active = true
        };
        var hiddenPerson = new Person
        {
            FirstName = "Skryty", LastName = "Zamestnanec", UID = "11",
            Gender = "M", CountryCode = "CZ", Active = true
        };
        _context.Persons.AddRange(visiblePerson, hiddenPerson);
        await _context.SaveChangesAsync();
        var visibleEmployee = new Employee { PersonId = visiblePerson.Id, PasswordHash = "x", Salt = "s" };
        var hiddenEmployee = new Employee { PersonId = hiddenPerson.Id, PasswordHash = "x", Salt = "s" };
        _context.Employees.AddRange(visibleEmployee, hiddenEmployee);
        await _context.SaveChangesAsync();

        _scopeMock
            .Setup(service => service.GetVisibleEmployeeIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<int> { visibleEmployee.Id });

        var response = await SearchAsync("Zamestnanec", patients: false, evaluations: false);

        Assert.Equal(visibleEmployee.Id, Assert.Single(response.Employees).Id);
    }

    [Fact]
    public async Task Search_Evaluation_ReturnsAnonymousMetadata()
    {
        var evaluation = new EvaluationCase
        {
            Title = "RTG hrudniku",
            SourceSide = "Left",
            SourcePosture = "Lying",
            ImageKey = "tdb"
        };
        _context.EvaluationCases.Add(evaluation);
        await _context.SaveChangesAsync();

        var response = await SearchAsync("hrudniku", patients: false, employees: false);

        var result = Assert.Single(response.Evaluations);
        Assert.Equal(evaluation.Id, result.CaseId);
        Assert.Equal("Left", result.Side);
    }
}
