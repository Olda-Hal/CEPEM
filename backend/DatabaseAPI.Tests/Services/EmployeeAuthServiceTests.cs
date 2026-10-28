using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using DatabaseAPI.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DatabaseAPI.Tests.Services;

/// <summary>
/// Risky-path coverage for <see cref="EmployeeAuthService"/>: brute-force
/// lockout (5 failures -&gt; 15 minute lock), counter resets, deactivated
/// accounts and password change rules.
/// </summary>
public class EmployeeAuthServiceTests : IDisposable
{
    private const string ValidPassword = "S3cure!Passw0rd";

    private readonly DatabaseContext _context;
    private readonly EmployeeAuthService _service;

    public EmployeeAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The in-memory store can't do real transactions; production uses MySQL.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = new DatabaseContext(options);

        var actorCountryMock = new Mock<IActorCountryContextService>();
        actorCountryMock.Setup(service => service.GetActorCountryCodeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("CZ");
        actorCountryMock.Setup(service => service.GetActorEmployeeId()).Returns(1);

        var uidMock = new Mock<IUidService>();
        uidMock.Setup(service => service.GetNextAvailableUidAsync()).ReturnsAsync("00000042");

        _service = new EmployeeAuthService(_context, actorCountryMock.Object, uidMock.Object);
    }

    [Fact]
    public async Task ResetPassword_ForMissingEmployee_ReturnsNull()
    {
        var management = new EmployeeManagementService(_context);

        var result = await management.ResetPasswordAsync(99999);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResetPassword_GeneratesUsablePassword_AndForcesChange()
    {
        var employee = SeedEmployee("jan@example.com", password: "OldPassword1");
        var management = new EmployeeManagementService(_context);

        var temporary = await management.ResetPasswordAsync(employee.Id);

        Assert.NotNull(temporary);
        Assert.Equal(12, temporary!.Length);
        await _context.Entry(employee).ReloadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(temporary, employee.PasswordHash));
        Assert.True(employee.PasswordExpiration < DateTime.UtcNow, "user must be forced to change the password");
        Assert.Equal(0, employee.FailedLoginAttempts);

        // Old password no longer works, the temporary one does
        var oldAuth = await _service.AuthenticateDetailedAsync("jan@example.com", "OldPassword1");
        Assert.NotEqual(AuthenticationResult.Success, oldAuth.Result);
        var newAuth = await _service.AuthenticateDetailedAsync("jan@example.com", temporary);
        Assert.Equal(AuthenticationResult.Success, newAuth.Result);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private Employee SeedEmployee(
        string email,
        string password = ValidPassword,
        string uid = "00000001",
        bool active = true,
        DateTime? passwordExpiration = null,
        string roleName = "Examiner")
    {
        var translation = new Translation { EN = roleName };
        var role = new Role { NameTranslation = translation };
        var person = new Person
        {
            FirstName = "Jan",
            LastName = "Novak",
            UID = uid,
            Gender = "M",
            CountryCode = "CZ",
            Active = active,
            CreatedAt = DateTime.UtcNow
        };
        _context.Persons.Add(person);
        _context.SaveChanges();

        var salt = BCrypt.Net.BCrypt.GenerateSalt();
        var employee = new Employee
        {
            PersonId = person.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, salt),
            Salt = salt,
            PasswordExpiration = passwordExpiration ?? DateTime.UtcNow.AddDays(90)
        };
        _context.Employees.Add(employee);

        _context.UserRoles.Add(new UserRole { UserId = person.Id, Role = role });

        var contact = new Contact();
        _context.Contacts.Add(contact);
        _context.SaveChanges();

        _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = email });
        _context.ContactToObjects.Add(new ContactToObject
        {
            ContactId = contact.Id,
            ObjectId = person.Id,
            ObjectType = ContactObjectType.Person,
            PersonId = person.Id
        });
        _context.SaveChanges();

        return employee;
    }

    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsSuccess()
    {
        SeedEmployee("jan@example.com");

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.Success, result.Result);
        Assert.NotNull(result.EmployeeInfo);
        Assert.Equal("jan@example.com", result.EmployeeInfo!.Email);
        Assert.Contains("Examiner", result.EmployeeInfo.Roles);
    }

    [Fact]
    public async Task Authenticate_WithUnknownEmail_ReturnsUserNotFound()
    {
        SeedEmployee("jan@example.com");

        var result = await _service.AuthenticateDetailedAsync("nobody@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.UserNotFound, result.Result);
        Assert.Null(result.EmployeeInfo);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("x", null)]
    public async Task Authenticate_WithEmptyCredentials_ReturnsInvalidCredentials(string? email, string? password)
    {
        var result = await _service.AuthenticateDetailedAsync(email!, password!);

        Assert.Equal(AuthenticationResult.InvalidCredentials, result.Result);
    }

    [Fact]
    public async Task Authenticate_WithWrongPassword_IncrementsFailedLoginAttempts()
    {
        var employee = SeedEmployee("jan@example.com");

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", "wrong-password");

        Assert.Equal(AuthenticationResult.InvalidCredentials, result.Result);
        await _context.Entry(employee).ReloadAsync();
        Assert.Equal(1, employee.FailedLoginAttempts);
        Assert.Null(employee.LockedUntil);
    }

    [Fact]
    public async Task Authenticate_AfterFiveFailedAttempts_LocksAccountForFifteenMinutes()
    {
        var employee = SeedEmployee("jan@example.com");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _service.AuthenticateDetailedAsync("jan@example.com", "wrong-password");
        }

        await _context.Entry(employee).ReloadAsync();
        Assert.Equal(0, employee.FailedLoginAttempts); // counter resets when the lock is applied
        Assert.NotNull(employee.LockedUntil);
        Assert.True(employee.LockedUntil > DateTime.UtcNow.AddMinutes(10));
        Assert.True(employee.LockedUntil <= DateTime.UtcNow.AddMinutes(15).AddSeconds(5));
    }

    [Fact]
    public async Task Authenticate_WhenLocked_ReturnsAccountLocked_EvenWithCorrectPassword()
    {
        var employee = SeedEmployee("jan@example.com");
        employee.LockedUntil = DateTime.UtcNow.AddMinutes(5);
        await _context.SaveChangesAsync();

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.AccountLocked, result.Result);
        Assert.Contains("uzamčen", result.Message);
    }

    [Fact]
    public async Task Authenticate_AfterLockExpires_AllowsLogin()
    {
        var employee = SeedEmployee("jan@example.com");
        employee.LockedUntil = DateTime.UtcNow.AddMinutes(-1); // already expired
        employee.FailedLoginAttempts = 0;
        await _context.SaveChangesAsync();

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.Success, result.Result);
        await _context.Entry(employee).ReloadAsync();
        Assert.Null(employee.LockedUntil);
        Assert.Equal(0, employee.FailedLoginAttempts);
    }

    [Fact]
    public async Task Authenticate_SuccessfulLogin_ResetsFailedAttemptCounter()
    {
        var employee = SeedEmployee("jan@example.com");
        employee.FailedLoginAttempts = 3;
        await _context.SaveChangesAsync();

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.Success, result.Result);
        await _context.Entry(employee).ReloadAsync();
        Assert.Equal(0, employee.FailedLoginAttempts);
    }

    [Fact]
    public async Task Authenticate_DeactivatedAccount_ReturnsAccountDeactivated()
    {
        SeedEmployee("jan@example.com", active: false);

        var result = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);

        Assert.Equal(AuthenticationResult.AccountDeactivated, result.Result);
    }

    [Fact]
    public async Task UpdateLastLogin_SetsTimestamp()
    {
        var employee = SeedEmployee("jan@example.com");

        await _service.UpdateLastLoginAsync(employee.Id);

        await _context.Entry(employee).ReloadAsync();
        Assert.NotNull(employee.LastLoginAt);
        Assert.True(employee.LastLoginAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsInvalidCurrentPassword()
    {
        var employee = SeedEmployee("jan@example.com");

        var result = await _service.ChangePasswordDetailedAsync(employee.Id, "wrong-old", "NewPass123");

        Assert.Equal(PasswordChangeResult.InvalidCurrentPassword, result.Result);
    }

    [Fact]
    public async Task ChangePassword_WithSamePassword_ReturnsSamePassword()
    {
        var employee = SeedEmployee("jan@example.com");

        var result = await _service.ChangePasswordDetailedAsync(employee.Id, ValidPassword, ValidPassword);

        Assert.Equal(PasswordChangeResult.SamePassword, result.Result);
    }

    [Fact]
    public async Task ChangePassword_WithValidData_ChangesHashAndExtendsExpiration()
    {
        var employee = SeedEmployee("jan@example.com", passwordExpiration: DateTime.UtcNow.AddDays(3));
        var oldHash = employee.PasswordHash;

        var result = await _service.ChangePasswordDetailedAsync(employee.Id, ValidPassword, "NewPass123!");

        Assert.Equal(PasswordChangeResult.Success, result.Result);
        await _context.Entry(employee).ReloadAsync();
        Assert.NotEqual(oldHash, employee.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123!", employee.PasswordHash));
        Assert.True(employee.PasswordExpiration > DateTime.UtcNow.AddDays(80));

        // old password no longer authenticates, new one does
        var oldAuth = await _service.AuthenticateDetailedAsync("jan@example.com", ValidPassword);
        Assert.NotEqual(AuthenticationResult.Success, oldAuth.Result);
        var newAuth = await _service.AuthenticateDetailedAsync("jan@example.com", "NewPass123!");
        Assert.Equal(AuthenticationResult.Success, newAuth.Result);
    }

    [Fact]
    public async Task ChangePassword_ForMissingEmployee_ReturnsValidationError()
    {
        var result = await _service.ChangePasswordDetailedAsync(99999, "whatever", "NewPass123!");

        Assert.Equal(PasswordChangeResult.ValidationError, result.Result);
    }

    [Fact]
    public async Task CreateEmployee_WithoutRoles_AssignsBaselineExaminerRole()
    {
        var examinerRole = new Role { NameTranslation = new Translation { EN = "Examiner" } };
        _context.Roles.Add(examinerRole);
        await _context.SaveChangesAsync();

        var result = await _service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            FirstName = "Nina",
            LastName = "Modra",
            Email = "nina@example.com",
            PhoneNumber = "777",
            UID = "00000050",
            Gender = "F",
            Password = "Password1"
        });

        Assert.NotNull(result);
        var personWithRolesCount = await _context.UserRoles
            .Where(ur => ur.UserId == result!.PersonId)
            .ToListAsync();
        Assert.Single(personWithRolesCount);
        Assert.Equal(examinerRole.Id, personWithRolesCount[0].RoleId);
    }

    [Fact]
    public async Task CreateEmployee_WithExplicitRoles_AssignsThoseRoles()
    {
        var doctorRole = new Role { NameTranslation = new Translation { EN = "Doctor" } };
        var examinerRole = new Role { NameTranslation = new Translation { EN = "Examiner" } };
        _context.Roles.AddRange(doctorRole, examinerRole);
        await _context.SaveChangesAsync();

        var result = await _service.CreateEmployeeAsync(new CreateEmployeeRequest
        {
            FirstName = "Nina",
            LastName = "Modra",
            Email = "nina@example.com",
            PhoneNumber = "777",
            UID = "00000051",
            Gender = "F",
            Password = "Password1",
            RoleIds = new List<int> { doctorRole.Id }
        });

        Assert.NotNull(result);
        var assignedRoleId = await _context.UserRoles
            .Where(ur => ur.UserId == result!.PersonId)
            .Select(ur => ur.RoleId)
            .SingleAsync();
        Assert.Equal(doctorRole.Id, assignedRoleId);
    }

    [Fact]
    public async Task CreateEmployee_WithDuplicateEmail_ReturnsNull()
    {
        SeedEmployee("jan@example.com", uid: "00000001");

        var request = new CreateEmployeeRequest
        {
            FirstName = "Petr",
            LastName = "Svoboda",
            Email = "jan@example.com",
            PhoneNumber = "123456789",
            UID = "00000002",
            Gender = "M",
            Password = "Password1"
        };

        var result = await _service.CreateEmployeeAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateEmployee_WithDuplicateUid_ReturnsNull()
    {
        SeedEmployee("jan@example.com", uid: "00000001");

        var request = new CreateEmployeeRequest
        {
            FirstName = "Petr",
            LastName = "Svoboda",
            Email = "petr@example.com",
            PhoneNumber = "123456789",
            UID = "00000001",
            Gender = "M",
            Password = "Password1"
        };

        var result = await _service.CreateEmployeeAsync(request);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateEmployee_WithValidRequest_CreatesPersonAndEmployee_ForcingPasswordChange()
    {
        var request = new CreateEmployeeRequest
        {
            FirstName = "Petr",
            LastName = "Svoboda",
            Email = "petr@example.com",
            PhoneNumber = "123456789",
            UID = "00000002",
            Gender = "M",
            Password = "Password1"
        };

        var result = await _service.CreateEmployeeAsync(request);

        Assert.NotNull(result);
        var employee = await _context.Employees
            .Include(e => e.Person)
            .SingleAsync(e => e.Id == result!.EmployeeId);
        Assert.Equal("Petr", employee.Person.FirstName);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password1", employee.PasswordHash));
        Assert.True(employee.PasswordExpiration < DateTime.UtcNow, "new accounts must be forced to change the password on first login");
        Assert.Equal(request.UID, employee.Person.UID);

        var contactEmails = await _context.ContactToObjects
            .Where(cto => cto.ObjectId == employee.PersonId)
            .Join(_context.ContactEmails, cto => cto.ContactId, ce => ce.ContactId, (_, ce) => ce.Email)
            .ToListAsync();
        Assert.Contains("petr@example.com", contactEmails);
    }
}
