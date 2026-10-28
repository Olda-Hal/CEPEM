using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

/// <summary>
/// Creation persistence for patients (person + contact + patient) following
/// the EventService pattern: the controller only binds HTTP to the service.
/// </summary>
public interface IPatientService
{
    Task<Patient> CreatePatientAsync(CreatePatientRequest request, string actorCountryCode);
}

public class PatientService : IPatientService
{
    private readonly DatabaseContext _context;
    private readonly IUidService _uidService;

    public PatientService(DatabaseContext context, IUidService uidService)
    {
        _context = context;
        _uidService = uidService;
    }

    public async Task<Patient> CreatePatientAsync(CreatePatientRequest request, string actorCountryCode)
    {
        var uid = request.Uid;
        if (string.IsNullOrWhiteSpace(uid) || !int.TryParse(uid, out _))
        {
            uid = await _uidService.GetNextAvailableUidAsync();
        }

        var person = new Person
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Gender = request.Gender,
            UID = uid,
            TitleBefore = request.TitleBefore,
            TitleAfter = request.TitleAfter,
            CountryCode = actorCountryCode,
            CreatedAt = DateTime.UtcNow,
            Active = true
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(request.Email) || !string.IsNullOrEmpty(request.PhoneNumber))
        {
            var contact = new Contact();
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(request.Email))
                _context.ContactEmails.Add(new ContactEmail { ContactId = contact.Id, Email = request.Email });
            if (!string.IsNullOrEmpty(request.PhoneNumber))
                _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = contact.Id, PhoneNumber = request.PhoneNumber });

            _context.ContactToObjects.Add(new ContactToObject
            {
                ContactId = contact.Id,
                ObjectId = person.Id,
                ObjectType = ContactObjectType.Person
            });
            await _context.SaveChangesAsync();
        }

        var patient = new Patient
        {
            PersonId = person.Id,
            BirthDate = request.BirthDate,
            InsuranceNumber = request.InsuranceNumber ?? 0,
            CountryCode = actorCountryCode,
            Alive = true
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return patient;
    }
}
