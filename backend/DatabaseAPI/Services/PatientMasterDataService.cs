using DatabaseAPI.APIModels;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Services;

/// <summary>
/// Applies demographic/contact changes from an intake form to the patient
/// card, including the name/e-mail/phone history rows. It is shared by the
/// staff intake endpoint and by the approval of public-intake change requests.
/// </summary>
public class PatientMasterDataService
{
    private readonly DatabaseContext _context;

    public PatientMasterDataService(DatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// True when the submitted form would change the patient card. Used to
    /// decide whether a public-intake approval request is needed.
    /// </summary>
    public bool HasMasterDataChanges(Patient patient, CreateIntakeFormEventRequest request)
    {
        if (!string.IsNullOrEmpty(request.FirstName) && request.FirstName != patient.Person.FirstName)
            return true;
        if (!string.IsNullOrEmpty(request.LastName) && request.LastName != patient.Person.LastName)
            return true;
        if (request.InsuranceNumber.HasValue && request.InsuranceNumber != patient.InsuranceNumber)
            return true;
        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.Date != patient.BirthDate.Date)
            return true;
        if (!string.IsNullOrWhiteSpace(request.Gender) && request.Gender != patient.Person.Gender)
            return true;

        var contact = patient.Person.ContactToObjects.FirstOrDefault()?.Contact;
        if (!string.IsNullOrEmpty(request.Email) &&
            contact?.Emails.FirstOrDefault()?.Email != request.Email)
        {
            return true;
        }
        if (!string.IsNullOrEmpty(request.PhoneNumber) &&
            contact?.PhoneNumbers.FirstOrDefault()?.PhoneNumber != request.PhoneNumber)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(request.Address) || !string.IsNullOrWhiteSpace(request.PostalCode))
        {
            var (street, city) = ParseAddress(request.Address);
            var address = patient.Person.Address;
            if (address == null)
                return true;
            if (!string.IsNullOrWhiteSpace(street) && address.Street != street)
                return true;
            if (!string.IsNullOrWhiteSpace(city) && address.City != city)
                return true;
            if (!string.IsNullOrWhiteSpace(request.PostalCode) && address.PostalCode != request.PostalCode)
                return true;
        }

        return false;
    }

    public async Task UpdatePatientDataAsync(Patient patient, CreateIntakeFormEventRequest request)
    {
        var hasChanges = false;

        // Update FirstName
        if (!string.IsNullOrEmpty(request.FirstName) && request.FirstName != patient.Person.FirstName)
        {
            // Close any open history record before adding a new one
            var openHistory = patient.Person.FirstNameHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
            if (openHistory == null)
            {
                var history = new FirstNameHistory
                {
                    PersonId = patient.Person.Id,
                    FirstName = patient.Person.FirstName,
                    UsedFrom = patient.Person.CreatedAt,
                    UsedTo = DateTime.UtcNow
                };
                _context.FirstNameHistories.Add(history);
            }

            patient.Person.FirstName = request.FirstName;
            hasChanges = true;
        }

        // Update LastName
        if (!string.IsNullOrEmpty(request.LastName) && request.LastName != patient.Person.LastName)
        {
            var openHistory = patient.Person.LastNameHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
            if (openHistory == null)
            {
                var history = new LastNameHistory
                {
                    PersonId = patient.Person.Id,
                    LastName = patient.Person.LastName,
                    UsedFrom = patient.Person.CreatedAt,
                    UsedTo = DateTime.UtcNow
                };
                _context.LastNameHistories.Add(history);
            }

            patient.Person.LastName = request.LastName;
            hasChanges = true;
        }

        // Update InsuranceNumber
        if (request.InsuranceNumber.HasValue && request.InsuranceNumber != patient.InsuranceNumber)
        {
            patient.InsuranceNumber = request.InsuranceNumber.Value;
            hasChanges = true;
        }

        if (request.DateOfBirth.HasValue && request.DateOfBirth.Value.Date != patient.BirthDate.Date)
        {
            patient.BirthDate = request.DateOfBirth.Value.Date;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(request.Gender) && request.Gender != patient.Person.Gender)
        {
            patient.Person.Gender = request.Gender;
            hasChanges = true;
        }

        // Update Email and PhoneNumber via Contact
        var contact = patient.Person.ContactToObjects.FirstOrDefault()?.Contact;
        
        if (!string.IsNullOrEmpty(request.Email) || !string.IsNullOrEmpty(request.PhoneNumber))
        {
            if (contact == null)
            {
                contact = new Contact();
                _context.Contacts.Add(contact);
                await _context.SaveChangesAsync();

                _context.ContactToObjects.Add(new ContactToObject
                {
                    ContactId = contact.Id,
                    ObjectId = patient.Person.Id,
                    ObjectType = ContactObjectType.Person,
                    PersonId = patient.Person.Id
                });
            }

            // Update Email
            if (!string.IsNullOrEmpty(request.Email))
            {
                var currentEmail = contact.Emails.FirstOrDefault();
                if (currentEmail == null)
                {
                    _context.ContactEmails.Add(new ContactEmail
                    {
                        ContactId = contact.Id,
                        Email = request.Email
                    });
                }
                else if (currentEmail.Email != request.Email)
                {
                    var openEmailHistory = patient.Person.EmailHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
                    if (openEmailHistory == null)
                    {
                        var emailHistory = new EmailHistory
                        {
                            PersonId = patient.Person.Id,
                            Email = currentEmail.Email,
                            UsedFrom = patient.Person.CreatedAt,
                            UsedTo = DateTime.UtcNow
                        };
                        _context.EmailHistories.Add(emailHistory);
                    }

                    currentEmail.Email = request.Email;
                }
                hasChanges = true;
            }

            // Update PhoneNumber
            if (!string.IsNullOrEmpty(request.PhoneNumber))
            {
                var currentPhone = contact.PhoneNumbers.FirstOrDefault();
                if (currentPhone == null)
                {
                    _context.ContactPhoneNumbers.Add(new ContactPhoneNumber
                    {
                        ContactId = contact.Id,
                        PhoneNumber = request.PhoneNumber
                    });
                }
                else if (currentPhone.PhoneNumber != request.PhoneNumber)
                {
                    var openPhoneHistory = patient.Person.PhoneNumberHistories.FirstOrDefault(h => h.UsedTo == DateTime.MaxValue || h.UsedTo > DateTime.UtcNow.Date.AddDays(1));
                    if (openPhoneHistory == null)
                    {
                        var phoneHistory = new PhoneNumberHistory
                        {
                            PersonId = patient.Person.Id,
                            PhoneNumber = currentPhone.PhoneNumber,
                            UsedFrom = patient.Person.CreatedAt,
                            UsedTo = DateTime.UtcNow
                        };
                        _context.PhoneNumberHistories.Add(phoneHistory);
                    }

                    currentPhone.PhoneNumber = request.PhoneNumber;
                }
                hasChanges = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Address) || !string.IsNullOrWhiteSpace(request.PostalCode))
        {
            var (street, city) = ParseAddress(request.Address);

            if (patient.Person.Address == null)
            {
                patient.Person.Address = new Address
                {
                    Street = street,
                    City = city,
                    PostalCode = request.PostalCode ?? string.Empty,
                    Country = patient.Person.CountryCode
                };
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(street))
                    patient.Person.Address.Street = street;

                if (!string.IsNullOrWhiteSpace(city))
                    patient.Person.Address.City = city;

                if (!string.IsNullOrWhiteSpace(request.PostalCode))
                    patient.Person.Address.PostalCode = request.PostalCode;

                if (string.IsNullOrWhiteSpace(patient.Person.Address.Country))
                    patient.Person.Address.Country = patient.Person.CountryCode;
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static (string street, string city) ParseAddress(string? rawAddress)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
            return (string.Empty, string.Empty);

        var segments = rawAddress.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return (string.Empty, string.Empty);

        if (segments.Length == 1)
            return (segments[0], string.Empty);

        return (segments[0], segments[1]);
    }
}
