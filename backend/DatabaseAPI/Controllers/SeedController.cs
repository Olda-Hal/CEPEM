using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DatabaseAPI.Data;
using DatabaseAPI.DatabaseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DatabaseAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly DatabaseContext _context;
        public SeedController(DatabaseContext context)
        {
            _context = context;
        }

        [HttpPost("admin")]
        public async Task<IActionResult> SeedAdmin()
        {
            // Check if admin already exists
            var existingAdmin = await _context.Employees
                .Include(e => e.Person)
                .ThenInclude(p => p.UserRoles)
                .ThenInclude(ur => ur.Role)
                .AnyAsync(e => e.Person.UserRoles.Any(ur => ur.Role.NameTranslation != null && ur.Role.NameTranslation.EN == "SysAdmin"));
                
            if (existingAdmin)
                return BadRequest("Admin already exists.");

            // Get or create SysAdmin role
            var adminRole = await _context.Roles
                .Include(r => r.NameTranslation)
                .FirstOrDefaultAsync(r => r.NameTranslation != null && r.NameTranslation.EN == "SysAdmin");
            if (adminRole == null)
            {
                var translation = new Translation { EN = "SysAdmin" };
                _context.Translations.Add(translation);
                await _context.SaveChangesAsync();
                adminRole = new Role { NameTranslationId = translation.Id };
                _context.Roles.Add(adminRole);
                await _context.SaveChangesAsync();
            }

            // Create Person for admin
            var adminPerson = new Person
            {
                FirstName = "Admin",
                LastName = "User", 
                UID = Guid.NewGuid().ToString(),
                Active = true,
                Gender = "M",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Persons.Add(adminPerson);
            await _context.SaveChangesAsync();

            // Create Contact with email and phone for admin
            var adminContact = new Contact();
            _context.Contacts.Add(adminContact);
            await _context.SaveChangesAsync();
            _context.ContactEmails.Add(new ContactEmail { ContactId = adminContact.Id, Email = "admin@cepem.local" });
            _context.ContactPhoneNumbers.Add(new ContactPhoneNumber { ContactId = adminContact.Id, PhoneNumber = "+420000000000" });
            _context.ContactToObjects.Add(new ContactToObject
            {
                ContactId = adminContact.Id,
                ObjectId = adminPerson.Id,
                ObjectType = ContactObjectType.Person
            });
            await _context.SaveChangesAsync();

            var password = "Admin123!";
            var passwordHash = HashPassword(password);
            
            // Create Employee
            var adminEmployee = new Employee
            {
                PersonId = adminPerson.Id,
                PasswordHash = passwordHash,
                Salt = "",
                PasswordExpiration = new DateTime(2000, 1, 1),
                LastLoginAt = null
            };
            
            _context.Employees.Add(adminEmployee);
            await _context.SaveChangesAsync();

            // Create UserRole
            var userRole = new UserRole
            {
                UserId = adminPerson.Id,
                RoleId = adminRole.Id
            };
            
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();
            
            return Ok("Admin user created with password: " + password);
        }

        [HttpPost("event-data")]
        public async Task<IActionResult> SeedEventData()
        {
            // Check if event data already exists
            if (await _context.EventTypes.AnyAsync())
                return BadRequest("Event data already exists.");

            // Seed Event Types
            var eventTypeData = new[] {
                (CS: "Vstupní Formulář", EN: "Intake Form"),
                (CS: "Návštěva",    EN: "Visit"),
                (CS: "Operace",     EN: "Surgery"),
                (CS: "Pohotovost",  EN: "Emergency"),
                (CS: "Kontrola",    EN: "Check-up"),
                (CS: "Vyšetření",   EN: "Examination"),
                (CS: "Lék",         EN: "Medication"),
                (CS: "Úraz",        EN: "Injury"),
                (CS: "Očkování",    EN: "Vaccination"),
                (CS: "Příznak",     EN: "Symptom"),
                (CS: "Těhotenství", EN: "Pregnancy"),
            };
            foreach (var d in eventTypeData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.EventTypes.Add(new EventType { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Drugs
            var drugData = new[] {
                (CS: "Paracetamol", EN: "Paracetamol"),
                (CS: "Ibuprofen",   EN: "Ibuprofen"),
                (CS: "Aspirin",     EN: "Aspirin"),
                (CS: "Antibiotika", EN: "Antibiotics"),
                (CS: "Inzulín",     EN: "Insulin"),
            };
            foreach (var d in drugData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.Drugs.Add(new Drug { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Drug Categories
            var drugCategoryData = new[] {
                (CS: "Bolest",               EN: "Pain"),
                (CS: "Teplota",              EN: "Fever"),
                (CS: "Zánět",                EN: "Inflammation"),
                (CS: "Infekce",              EN: "Infection"),
                (CS: "Prevence",             EN: "Prevention"),
                (CS: "Chronické onemocnění", EN: "Chronic disease"),
            };
            foreach (var d in drugCategoryData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.DrugCategories.Add(new DrugCategory { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Examination Types
            var examinationTypeData = new[] {
                (CS: "Krevní test", EN: "Blood Test"),
                (CS: "Rentgen",     EN: "X-Ray"),
                (CS: "MRI",         EN: "MRI"),
                (CS: "CT",          EN: "CT"),
                (CS: "Ultrazvuk",   EN: "Ultrasound"),
            };
            foreach (var d in examinationTypeData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.ExaminationTypes.Add(new ExaminationType { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Symptoms
            var symptomData = new[] {
                (CS: "Horečka",      EN: "Fever"),
                (CS: "Bolest hlavy", EN: "Headache"),
                (CS: "Kašel",        EN: "Cough"),
                (CS: "Nevolnost",    EN: "Nausea"),
                (CS: "Únava",        EN: "Fatigue"),
            };
            foreach (var d in symptomData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.Symptoms.Add(new Symptom { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Injury Types
            var injuryTypeData = new[] {
                (CS: "Zlomenina",   EN: "Fracture"),
                (CS: "Podvrtnutí",  EN: "Sprain"),
                (CS: "Řezná rána",  EN: "Cut"),
                (CS: "Popálenina",  EN: "Burn"),
                (CS: "Modřina",     EN: "Bruise"),
            };
            foreach (var d in injuryTypeData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.InjuryTypes.Add(new InjuryType { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();

            // Seed Vaccine Types
            var vaccineTypeData = new[] {
                (CS: "COVID-19",     EN: "COVID-19"),
                (CS: "Chřipka",      EN: "Influenza"),
                (CS: "Tetanus",      EN: "Tetanus"),
                (CS: "Hepatitida B", EN: "Hepatitis B"),
                (CS: "MMR",          EN: "MMR"),
            };
            foreach (var d in vaccineTypeData)
            {
                var t = new Translation { EN = d.EN, CS = d.CS };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.VaccineTypes.Add(new VaccineType { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();
            
            return Ok("Event data seeded successfully.");
        }

        [HttpPost("add-event-types")]
        public async Task<IActionResult> AddEventTypes([FromBody] string[] eventTypeNames)
        {
            foreach (var name in eventTypeNames)
            {
                var t = new Translation { EN = name };
                _context.Translations.Add(t);
                await _context.SaveChangesAsync();
                _context.EventTypes.Add(new EventType { NameTranslationId = t.Id });
            }
            await _context.SaveChangesAsync();
            return Ok($"Added {eventTypeNames.Length} event types.");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
