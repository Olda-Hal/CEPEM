using DatabaseAPI.Controllers;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DatabaseAPI.Tests.Controllers;

/// <summary>
/// The effective-permission endpoint is the heart of the authorization
/// model (every gated request goes through it). Covers deny/allow order:
/// employee deny &gt; employee allow &gt; role deny &gt; role allow &gt; default deny,
/// plus scoped grants.
/// </summary>
public class PermissionsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly PermissionsController _controller;

    public PermissionsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _controller = new PermissionsController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private (Employee employee, Role role, Permission permission) SeedEmployeeWithRole(string permissionCode = "patients.read", string effect = "Allow")
    {
        var person = new Person
        {
            FirstName = "Jan", LastName = "Novak", UID = "00000001", Gender = "M", CountryCode = "CZ", Active = true
        };
        var employee = new Employee { Person = person, PasswordHash = "x", Salt = "s" };
        var role = new Role { NameTranslation = new Translation { EN = "Doctor" } };
        var permission = new Permission { Code = permissionCode, Description = permissionCode };
        _context.AddRange(employee, role, permission);
        _context.SaveChanges();
        _context.UserRoles.Add(new UserRole { UserId = person.Id, RoleId = role.Id });
        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, Effect = effect });
        _context.SaveChanges();
        return (employee, role, permission);
    }

    private static (bool Allowed, string Reason) ReadEffective(ActionResult<object> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        return (doc.RootElement.GetProperty("allowed").GetBoolean(), doc.RootElement.GetProperty("reason").GetString() ?? "");
    }

    [Fact]
    public async Task Effective_WithUnknownEmployee_ReturnsNotFound()
    {
        _context.Permissions.Add(new Permission { Code = "patients.read" });
        await _context.SaveChangesAsync();

        var result = await _controller.Effective(99999, "patients.read");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Effective_WithUnknownPermission_ReturnsNotFound()
    {
        var (employee, _, _) = SeedEmployeeWithRole();

        var result = await _controller.Effective(employee.Id, "does.not.exist");

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Effective_DefaultDeny_WhenNoGrantExists()
    {
        var person = new Person { FirstName = "Jan", LastName = "N", UID = "1", Gender = "M", CountryCode = "CZ", Active = true };
        var employee = new Employee { Person = person, PasswordHash = "x", Salt = "s" };
        var permission = new Permission { Code = "audit.read" };
        _context.AddRange(employee, permission);
        await _context.SaveChangesAsync();

        var (allowed, reason) = ReadEffective(await _controller.Effective(employee.Id, "audit.read"));

        Assert.False(allowed);
        Assert.Equal("Default deny", reason);
    }

    [Fact]
    public async Task Effective_RoleAllow_GrantsAccess()
    {
        var (employee, _, _) = SeedEmployeeWithRole(effect: "Allow");

        var (allowed, reason) = ReadEffective(await _controller.Effective(employee.Id, "patients.read"));

        Assert.True(allowed);
        Assert.Equal("Role allow", reason);
    }

    [Fact]
    public async Task Effective_RoleDeny_BeatsRoleAllow()
    {
        var (employee, role, permission) = SeedEmployeeWithRole(effect: "Allow");
        var denyRole = new Role { NameTranslation = new Translation { EN = "Deny role" } };
        _context.Roles.Add(denyRole);
        await _context.SaveChangesAsync();
        _context.UserRoles.Add(new UserRole { UserId = employee.PersonId, RoleId = denyRole.Id });
        _context.RolePermissions.Add(new RolePermission { RoleId = denyRole.Id, PermissionId = permission.Id, Effect = "Deny" });
        await _context.SaveChangesAsync();

        var (allowed, reason) = ReadEffective(await _controller.Effective(employee.Id, "patients.read"));

        Assert.False(allowed);
        Assert.Equal("Role deny", reason);
    }

    [Fact]
    public async Task Effective_EmployeeAllow_BeatsRoleDeny()
    {
        var (employee, role, permission) = SeedEmployeeWithRole(effect: "Deny");
        _context.EmployeePermissions.Add(new EmployeePermission
        {
            UserId = employee.PersonId,
            PermissionId = permission.Id,
            Effect = "Allow"
        });
        await _context.SaveChangesAsync();

        var (allowed, reason) = ReadEffective(await _controller.Effective(employee.Id, "patients.read"));

        Assert.True(allowed);
        Assert.Equal("Explicit employee allow", reason);
    }

    [Fact]
    public async Task Effective_EmployeeDeny_BeatsEverything()
    {
        var (employee, _, permission) = SeedEmployeeWithRole(effect: "Allow");
        _context.EmployeePermissions.Add(new EmployeePermission
        {
            UserId = employee.PersonId,
            PermissionId = permission.Id,
            Effect = "Deny"
        });
        await _context.SaveChangesAsync();

        var (allowed, reason) = ReadEffective(await _controller.Effective(employee.Id, "patients.read"));

        Assert.False(allowed);
        Assert.Equal("Explicit employee deny", reason);
    }

    [Fact]
    public async Task Effective_ScopedAllow_OnlyAppliesToMatchingScope()
    {
        var (employee, _, permission) = SeedEmployeeWithRole(effect: "Deny"); // role denies globally
        _context.EmployeePermissions.Add(new EmployeePermission
        {
            UserId = employee.PersonId,
            PermissionId = permission.Id,
            Effect = "Allow",
            ScopeType = "Hospital",
            ScopeId = 7
        });
        await _context.SaveChangesAsync();

        var matching = ReadEffective(await _controller.Effective(employee.Id, "patients.read", "Hospital", 7));
        var otherScope = ReadEffective(await _controller.Effective(employee.Id, "patients.read", "Hospital", 8));
        var noScope = ReadEffective(await _controller.Effective(employee.Id, "patients.read"));

        Assert.True(matching.Allowed);
        Assert.False(otherScope.Allowed);
        Assert.False(noScope.Allowed);
    }

    [Fact]
    public async Task GetAll_ReturnsPermissions()
    {
        _context.Permissions.AddRange(
            new Permission { Code = "patients.read" },
            new Permission { Code = "patients.write" });
        await _context.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(2, Assert.IsType<List<Permission>>(ok.Value).Count);
    }

    [Fact]
    public async Task GetEmployeePermissions_WithUnknownEmployee_ReturnsNotFound()
    {
        var result = await _controller.GetEmployeePermissions(99999);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateRolePermissions_ReplacesSet_AndNormalizesEffects()
    {
        var (employee, role, permission) = SeedEmployeeWithRole();
        var second = new Permission { Code = "audit.read" };
        _context.Permissions.Add(second);
        await _context.SaveChangesAsync();

        var result = await _controller.UpdateRolePermissions(role.Id, new PermissionsController.UpdateRolePermissionsRequest
        {
            Permissions = new List<PermissionsController.RolePermissionDto>
            {
                new() { PermissionId = second.Id, Code = "audit.read", Effect = "allow" },
                new() { PermissionId = permission.Id, Code = "patients.read", Effect = "DENY" }
            }
        });

        Assert.IsType<OkObjectResult>(result);
        var updated = await _context.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();
        Assert.Equal(2, updated.Count);
        Assert.Equal("Allow", updated.Single(rp => rp.PermissionId == second.Id).Effect);
        Assert.Equal("Deny", updated.Single(rp => rp.PermissionId == permission.Id).Effect);
    }

    [Fact]
    public async Task UpdateRolePermissions_WithUnknownPermissionId_ReturnsBadRequest()
    {
        var (_, role, _) = SeedEmployeeWithRole();

        var result = await _controller.UpdateRolePermissions(role.Id, new PermissionsController.UpdateRolePermissionsRequest
        {
            Permissions = new List<PermissionsController.RolePermissionDto>
            {
                new() { PermissionId = 99999, Code = "ghost", Effect = "Allow" }
            }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task EmployeePermission_CreateAndDelete_Roundtrip()
    {
        var (employee, _, permission) = SeedEmployeeWithRole();

        var createResult = await _controller.CreateEmployeePermission(new PermissionsController.CreateEmployeePermissionRequest
        {
            EmployeeId = employee.Id,
            PermissionId = permission.Id,
            Effect = "Deny"
        });

        var ok = Assert.IsType<OkObjectResult>(createResult.Result);
        var created = Assert.IsType<EmployeePermission>(ok.Value);
        Assert.Equal(employee.PersonId, created.UserId);

        var deleteResult = await _controller.DeleteEmployeePermission(created.Id);
        Assert.IsType<OkResult>(deleteResult);
        Assert.False(await _context.EmployeePermissions.AnyAsync(ep => ep.Id == created.Id));
    }
}

public class ActivityLogsControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly ActivityLogsController _controller;

    public ActivityLogsControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _controller = new ActivityLogsController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedLogs(int count, string action = "GET /api/patients", int? employeeId = 1, string email = "a@example.com")
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < count; i++)
        {
            _context.ActivityLogs.Add(new ActivityLog
            {
                Service = "HealthcareAPI",
                Action = action,
                EmployeeId = employeeId,
                EmployeeEmail = email,
                Method = "GET",
                StatusCode = 200,
                Timestamp = start.AddHours(i)
            });
        }
        _context.SaveChanges();
    }

    private static ActivityLogsController.ActivityLogListResponse Unwrap(ActionResult<ActivityLogsController.ActivityLogListResponse> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<ActivityLogsController.ActivityLogListResponse>(ok.Value);
    }

    [Fact]
    public async Task GetLogs_ReturnsPagedResults_NewestFirst()
    {
        SeedLogs(5);

        var response = Unwrap(await _controller.GetLogs(page: 0, limit: 2));

        Assert.Equal(2, response.Logs.Count);
        Assert.Equal(5, response.TotalCount);
        Assert.True(response.HasMore);
        Assert.True(response.Logs[0].Timestamp > response.Logs[1].Timestamp);
    }

    [Fact]
    public async Task GetLogs_ClampsInvalidLimitToDefault()
    {
        SeedLogs(60);

        var response = Unwrap(await _controller.GetLogs(page: 0, limit: 99999));

        Assert.Equal(50, response.Logs.Count);
    }

    [Fact]
    public async Task GetLogs_FiltersByEmployeeAndSearch()
    {
        SeedLogs(2, employeeId: 1, email: "admin@example.com");
        SeedLogs(1, action: "POST /api/events", employeeId: 2, email: "nurse@example.com");

        var byEmployee = Unwrap(await _controller.GetLogs(employeeId: 2));
        Assert.Single(byEmployee.Logs);

        var bySearch = Unwrap(await _controller.GetLogs(search: "events"));
        Assert.Single(bySearch.Logs);

        var byEmailSearch = Unwrap(await _controller.GetLogs(search: "ADMIN@"));
        Assert.Equal(2, byEmailSearch.Logs.Count);
    }

    [Fact]
    public async Task GetLogs_FiltersByDateRange()
    {
        SeedLogs(10);

        var from = new DateTime(2026, 10, 1, 2, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc);
        var response = Unwrap(await _controller.GetLogs(from: from, to: to));

        Assert.InRange(response.Logs.Count, 3, 4);
    }

    [Fact]
    public async Task LogActivity_StampsServerTime()
    {
        var result = await _controller.LogActivity(new ActivityLog
        {
            Service = "HealthcareAPI",
            Action = "/api/patients",
            Timestamp = DateTime.MinValue
        });

        Assert.IsType<OkResult>(result);
        var saved = await _context.ActivityLogs.SingleAsync();
        Assert.True(saved.Timestamp > DateTime.UtcNow.AddMinutes(-1));
    }
}

public class EmployeesControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly EmployeesController _controller;

    public EmployeesControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _controller = new EmployeesController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Employee SeedEmployee(bool active = true, string email = "jan@example.com")
    {
        var person = new Person
        {
            FirstName = "Jan", LastName = "Novak", UID = "1", Gender = "M", CountryCode = "CZ", Active = active
        };
        var employee = new Employee { Person = person, PasswordHash = "SECRETHASH", Salt = "SECRETSALT", LastLoginAt = DateTime.UtcNow.AddDays(-2) };
        _context.Employees.Add(employee);
        _context.SaveChanges();

        var contact = new Contact();
        _context.Contacts.Add(contact);
        _context.SaveChanges();
        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = email });
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id, ObjectId = person.Id, ObjectType = ContactObjectType.Person, PersonId = person.Id
        });
        _context.SaveChanges();
        return employee;
    }

    [Fact]
    public async Task GetEmployee_ReturnsInfo_WithoutPasswordMaterial()
    {
        var employee = SeedEmployee();

        var result = await _controller.GetEmployee(employee.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var info = Assert.IsType<DatabaseAPI.APIModels.EmployeeAuthInfo>(ok.Value);
        Assert.Equal("Jan", info.FirstName);
        Assert.Equal("jan@example.com", info.Email);
        Assert.Equal(string.Empty, info.PasswordHash);
        Assert.Equal(string.Empty, info.Salt);
    }

    [Fact]
    public async Task GetEmployee_ForDeactivatedPerson_ReturnsNotFound()
    {
        var employee = SeedEmployee(active: false);

        var result = await _controller.GetEmployee(employee.Id);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetDashboardStats_CountsOnlyActiveEmployees()
    {
        SeedEmployee(active: true, email: "a@example.com");
        SeedEmployee(active: false, email: "b@example.com");

        var result = await _controller.GetDashboardStats(1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("TotalEmployees").GetInt32());
        Assert.Equal("Online", doc.RootElement.GetProperty("SystemStatus").GetString());
    }
}

public class HospitalExaminationTypesControllerTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly HospitalExaminationTypesController _controller;

    public HospitalExaminationTypesControllerTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);
        _context.Hospitals.Add(new Hospital { Id = 1, Name = "Test", Active = true });
        _context.SaveChanges();
        _controller = new HospitalExaminationTypesController(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private int SeedType(string en, string? cs = null) {
        var type = new ExaminationType { NameTranslation = new Translation { EN = en, CS = cs } };
        _context.ExaminationTypes.Add(type);
        _context.SaveChanges();
        return type.Id;
    }

    [Fact]
    public async Task Allow_Twice_ReturnsConflict()
    {
        var typeId = SeedType("XRay");

        var first = await _controller.Allow(1, typeId);
        var second = await _controller.Allow(1, typeId);

        Assert.IsType<OkResult>(first);
        Assert.IsType<ConflictObjectResult>(second);
    }

    [Fact]
    public async Task Allow_ForUnknownHospital_ReturnsNotFound()
    {
        var typeId = SeedType("XRay");

        Assert.IsType<NotFoundObjectResult>(await _controller.Allow(99999, typeId));
    }

    [Fact]
    public async Task SetAll_ReplacesExisting_AndSkipsUnknownIds()
    {
        var typeId = SeedType("XRay", cs: "Rentgen");
        var oldTypeId = SeedType("Old");
        _context.HospitalExaminationTypes.Add(new HospitalExaminationType { HospitalId = 1, ExaminationTypeId = oldTypeId });
        await _context.SaveChangesAsync();

        var result = await _controller.SetAll(1, new[] { typeId, 99999 });

        Assert.IsType<OkResult>(result);
        var remaining = await _context.HospitalExaminationTypes.Where(het => het.HospitalId == 1).ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(typeId, remaining[0].ExaminationTypeId);
    }

    [Fact]
    public async Task GetAllowed_ResolvesLanguage_FallingBackToEnglish()
    {
        var typeId = SeedType("XRay", cs: "Rentgen");
        await _controller.Allow(1, typeId);

        var czech = await _controller.GetAllowed(1, "cs");
        var czechName = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(czech.Result).Value);
        Assert.Contains("Rentgen", czechName);

        var dutch = await _controller.GetAllowed(1, "nl"); // NL missing -> falls back to EN
        var dutchName = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(dutch.Result).Value);
        Assert.Contains("XRay", dutchName);
    }

    [Fact]
    public async Task Disallow_ForMissingEntry_ReturnsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(await _controller.Disallow(1, 99999));
    }
}

public class ExaminationDocumentsTests : IDisposable
{
    private readonly DatabaseContext _context;
    private readonly ExaminationDocumentService _service;
    private readonly ExaminationDocumentsController _controller;
    private readonly string _storagePath;

    public ExaminationDocumentsTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new DatabaseContext(options);

        _storagePath = Path.Combine(Path.GetTempPath(), $"cepem-test-{Guid.NewGuid():N}");
        var config = new TestConfiguration(new Dictionary<string, string?>
        {
            ["DocumentStorage:Path"] = _storagePath,
            ["DocumentEncryption:Key"] = "test-encryption-key-32chars-long!"
        });

        var encryption = new DocumentEncryptionService(config, Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentEncryptionService>.Instance);
        _service = new ExaminationDocumentService(_context, encryption, config, Microsoft.Extensions.Logging.Abstractions.NullLogger<ExaminationDocumentService>.Instance);
        _controller = new ExaminationDocumentsController(_service, Microsoft.Extensions.Logging.Abstractions.NullLogger<ExaminationDocumentsController>.Instance);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        if (Directory.Exists(_storagePath))
            Directory.Delete(_storagePath, recursive: true);
    }

    private Examination SeedExamination()
    {
        var type = new ExaminationType { NameTranslation = new Translation { EN = "Blood" } };
        var eventType = new EventType { NameTranslation = new Translation { EN = "Visit" } };
        var person = new Person { FirstName = "Eva", LastName = "K", UID = "1", Gender = "F", CountryCode = "CZ" };
        var patient = new Patient { Person = person, BirthDate = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), InsuranceNumber = 1 };
        var ev = new Event { Patient = patient, EventType = eventType, HappenedAt = DateTime.UtcNow };
        var examination = new Examination { Event = ev, ExaminationType = type, CountryCode = "CZ" };
        _context.Examinations.Add(examination);
        _context.SaveChanges();
        return examination;
    }

    private Task<ExaminationDocument> SeedDocument(Examination examination)
    {
        // Goes through the real save path so the encrypted file exists on disk.
        return _service.SaveDocumentAsync(examination.Id, new byte[] { 1, 2, 3 }, "report.pdf");
    }

    [Fact]
    public async Task Upload_ForMissingExamination_ReturnsBadRequest()
    {
        var file = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "a.pdf");

        var result = await _controller.Upload(99999, file);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("not found", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Upload_WithEmptyFile_ReturnsBadRequest()
    {
        var result = await _controller.Upload(1, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Download_WithWrongExaminationId_ReturnsNotFound_IdorBlocked()
    {
        var examination = SeedExamination();
        var other = SeedExamination();
        var doc = await SeedDocument(examination);

        var result = await _controller.Download(other.Id, doc.Id);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Download_WithMatchingExamination_ReturnsDecryptedContent()
    {
        var examination = SeedExamination();
        var stored = await _service.SaveDocumentAsync(examination.Id, new byte[] { 10, 20, 30 }, "report.pdf");

        var result = await _controller.Download(examination.Id, stored.Id);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(new byte[] { 10, 20, 30 }, file.FileContents);
        Assert.Equal("report.pdf", file.FileDownloadName);
    }

    [Fact]
    public async Task Delete_WithWrongExaminationId_ReturnsNotFound_IdorBlocked()
    {
        var examination = SeedExamination();
        var other = SeedExamination();
        var doc = await SeedDocument(examination);

        var result = await _controller.Delete(other.Id, doc.Id);

        Assert.IsType<NotFoundObjectResult>(result);
        var persisted = await _context.ExaminationDocuments.SingleAsync(d => d.Id == doc.Id);
        Assert.False(persisted.IsDeleted);
    }

    [Fact]
    public async Task Delete_MarksDocumentDeleted_AndHidesFromListing()
    {
        var examination = SeedExamination();
        var doc = await SeedDocument(examination);

        var result = await _controller.Delete(examination.Id, doc.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.True((await _context.ExaminationDocuments.FindAsync(doc.Id))!.IsDeleted);
        var listing = await _service.GetDocumentsAsync(examination.Id);
        Assert.Empty(listing);
    }

    [Fact]
    public async Task SaveDocument_OverSizeLimit_Throws()
    {
        var examination = SeedExamination();
        var bigChunks = new byte[10 * 1024 * 1024 + 1];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SaveDocumentAsync(examination.Id, bigChunks, "big.bin"));
    }

    /// <summary>
    /// Minimal IConfiguration backed by a dictionary — avoids pulling the
    /// Microsoft.Extensions.Configuration.Memory package into the test project.
    /// </summary>
    private sealed class TestConfiguration : Microsoft.Extensions.Configuration.IConfiguration
    {
        private readonly Dictionary<string, string?> _values;

        public TestConfiguration(Dictionary<string, string?> values) => _values = values;

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var value) ? value : null;
            set => _values[key] = value;
        }

        public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
            _values.Keys.Select(key => (Microsoft.Extensions.Configuration.IConfigurationSection)
                new TestSection(key, _values[key]));

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

        public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
            new TestSection(key, _values.TryGetValue(key, out var value) ? value : null);

        private sealed class TestSection : Microsoft.Extensions.Configuration.IConfigurationSection
        {
            public TestSection(string key, string? value)
            {
                Key = key;
                Path = key;
                Value = value;
            }

            public string Key { get; }
            public string Path { get; }
            public string? Value { get; set; }

            public string? this[string key]
            {
                get => key == Key ? Value : null;
                set => Value = value;
            }

            public IEnumerable<Microsoft.Extensions.Configuration.IConfigurationSection> GetChildren() =>
                Enumerable.Empty<Microsoft.Extensions.Configuration.IConfigurationSection>();

            public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
                new Microsoft.Extensions.Primitives.CancellationChangeToken(CancellationToken.None);

            public Microsoft.Extensions.Configuration.IConfigurationSection GetSection(string key) =>
                new TestSection(key, null);
        }
    }
}
