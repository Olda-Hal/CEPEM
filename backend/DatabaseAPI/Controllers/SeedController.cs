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
                .AnyAsync(e => e.Person.UserRoles.Any(ur => ur.Role.Name == "SysAdmin"));
                
            if (existingAdmin)
                return BadRequest("Admin already exists.");

            // Get or create SysAdmin role
            var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "SysAdmin");
            if (adminRole == null)
            {
                adminRole = new Role { Name = "SysAdmin" };
                _context.Roles.Add(adminRole);
                await _context.SaveChangesAsync();
            }

            // Create Person for admin
            var adminPerson = new Person
            {
                FirstName = "Admin",
                LastName = "User", 
                Email = "admin@cepem.local",
                PhoneNumber = "+420000000000",
                UID = Guid.NewGuid().ToString(),
                Active = true,
                Gender = "M",
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Persons.Add(adminPerson);
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
            var eventTypes = new[]
            {
                new EventType { Name = "Návštěva" },
                new EventType { Name = "Operace" }, 
                new EventType { Name = "Pohotovost" },
                new EventType { Name = "Kontrola" },
                new EventType { Name = "Vyšetření" },
                new EventType { Name = "Lék" },
                new EventType { Name = "Úraz" },
                new EventType { Name = "Očkování" },
                new EventType { Name = "Příznak" },
                new EventType { Name = "Těhotenství" }
            };
            _context.EventTypes.AddRange(eventTypes);
            await _context.SaveChangesAsync();

            // Seed Event Type Translations
            var eventTypeTranslations = new[]
            {
                new EventTypeTranslation { EventTypeId = eventTypes[0].Id, Language = "en", Name = "Visit" },
                new EventTypeTranslation { EventTypeId = eventTypes[1].Id, Language = "en", Name = "Surgery" },
                new EventTypeTranslation { EventTypeId = eventTypes[2].Id, Language = "en", Name = "Emergency" },
                new EventTypeTranslation { EventTypeId = eventTypes[3].Id, Language = "en", Name = "Check-up" },
                new EventTypeTranslation { EventTypeId = eventTypes[4].Id, Language = "en", Name = "Examination" },
                new EventTypeTranslation { EventTypeId = eventTypes[5].Id, Language = "en", Name = "Medication" },
                new EventTypeTranslation { EventTypeId = eventTypes[6].Id, Language = "en", Name = "Injury" },
                new EventTypeTranslation { EventTypeId = eventTypes[7].Id, Language = "en", Name = "Vaccination" },
                new EventTypeTranslation { EventTypeId = eventTypes[8].Id, Language = "en", Name = "Symptom" },
                new EventTypeTranslation { EventTypeId = eventTypes[9].Id, Language = "en", Name = "Pregnancy" }
            };
            _context.EventTypeTranslations.AddRange(eventTypeTranslations);

            // Seed Drugs
            var drugs = new[]
            {
                new Drug { Name = "Paracetamol" },
                new Drug { Name = "Ibuprofen" },
                new Drug { Name = "Aspirin" },
                new Drug { Name = "Antibiotika" },
                new Drug { Name = "Inzulín" }
            };
            _context.Drugs.AddRange(drugs);
            await _context.SaveChangesAsync();

            // Seed Drug Translations
            var drugTranslations = new[]
            {
                new DrugTranslation { DrugId = drugs[0].Id, Language = "en", Name = "Paracetamol" },
                new DrugTranslation { DrugId = drugs[1].Id, Language = "en", Name = "Ibuprofen" },
                new DrugTranslation { DrugId = drugs[2].Id, Language = "en", Name = "Aspirin" },
                new DrugTranslation { DrugId = drugs[3].Id, Language = "en", Name = "Antibiotics" },
                new DrugTranslation { DrugId = drugs[4].Id, Language = "en", Name = "Insulin" }
            };
            _context.DrugTranslations.AddRange(drugTranslations);

            // Seed Drug Categories
            var drugCategories = new[]
            {
                new DrugCategory { Name = "Bolest" },
                new DrugCategory { Name = "Teplota" },
                new DrugCategory { Name = "Zánět" },
                new DrugCategory { Name = "Infekce" },
                new DrugCategory { Name = "Prevence" },
                new DrugCategory { Name = "Chronické onemocnění" }
            };
            _context.DrugCategories.AddRange(drugCategories);

            // Seed Examination Types
            var examinationTypes = new[]
            {
                new ExaminationType { Name = "Krevní test" },
                new ExaminationType { Name = "Rentgen" },
                new ExaminationType { Name = "MRI" },
                new ExaminationType { Name = "CT" },
                new ExaminationType { Name = "Ultrazvuk" }
            };
            _context.ExaminationTypes.AddRange(examinationTypes);
            await _context.SaveChangesAsync();

            // Seed Examination Type Translations
            var examinationTypeTranslations = new[]
            {
                new ExaminationTypeTranslation { ExaminationTypeId = examinationTypes[0].Id, Language = "en", Name = "Blood Test" },
                new ExaminationTypeTranslation { ExaminationTypeId = examinationTypes[1].Id, Language = "en", Name = "X-Ray" },
                new ExaminationTypeTranslation { ExaminationTypeId = examinationTypes[2].Id, Language = "en", Name = "MRI" },
                new ExaminationTypeTranslation { ExaminationTypeId = examinationTypes[3].Id, Language = "en", Name = "CT" },
                new ExaminationTypeTranslation { ExaminationTypeId = examinationTypes[4].Id, Language = "en", Name = "Ultrasound" }
            };
            _context.ExaminationTypeTranslations.AddRange(examinationTypeTranslations);

            // Seed Symptoms
            var symptoms = new[]
            {
                new Symptom { Name = "Horečka" },
                new Symptom { Name = "Bolest hlavy" },
                new Symptom { Name = "Kašel" },
                new Symptom { Name = "Nevolnost" },
                new Symptom { Name = "Únava" }
            };
            _context.Symptoms.AddRange(symptoms);

            // Seed Injury Types
            var injuryTypes = new[]
            {
                new InjuryType { Name = "Zlomenina" },
                new InjuryType { Name = "Podvrtnutí" },
                new InjuryType { Name = "Řezná rána" },
                new InjuryType { Name = "Popálenina" },
                new InjuryType { Name = "Modřina" }
            };
            _context.InjuryTypes.AddRange(injuryTypes);

            // Seed Vaccine Types
            var vaccineTypes = new[]
            {
                new VaccineType { Name = "COVID-19" },
                new VaccineType { Name = "Chřipka" },
                new VaccineType { Name = "Tetanus" },
                new VaccineType { Name = "Hepatitida B" },
                new VaccineType { Name = "MMR" }
            };
            _context.VaccineTypes.AddRange(vaccineTypes);

            await _context.SaveChangesAsync();
            
            return Ok("Event data seeded successfully.");
        }

        [HttpPost("add-event-types")]
        public async Task<IActionResult> AddEventTypes([FromBody] string[] eventTypeNames)
        {
            foreach (var name in eventTypeNames)
            {
                var exists = await _context.EventTypes.AnyAsync(et => et.Name == name);
                if (!exists)
                {
                    _context.EventTypes.Add(new EventType { Name = name });
                }
            }
            
            await _context.SaveChangesAsync();
            return Ok($"Added {eventTypeNames.Length} event types.");
        }

        [HttpPost("translations")]
        public async Task<IActionResult> SeedTranslations()
        {
            var translationsAdded = 0;

            var eventTypeMap = new Dictionary<string, string>
            {
                { "Návštěva", "Visit" },
                { "Operace", "Surgery" },
                { "Pohotovost", "Emergency" },
                { "Kontrola", "Check-up" },
                { "Vyšetření", "Examination" },
                { "Lék", "Medication" },
                { "Úraz", "Injury" },
                { "Očkování", "Vaccination" },
                { "Příznak", "Symptom" },
                { "Těhotenství", "Pregnancy" },
                { "Léčba", "Treatment" }
            };

            foreach (var kvp in eventTypeMap)
            {
                var eventType = await _context.EventTypes.FirstOrDefaultAsync(et => et.Name == kvp.Key);
                if (eventType != null)
                {
                    var existingTranslation = await _context.EventTypeTranslations
                        .AnyAsync(ett => ett.EventTypeId == eventType.Id && ett.Language == "en");
                    
                    if (!existingTranslation)
                    {
                        _context.EventTypeTranslations.Add(new EventTypeTranslation
                        {
                            EventTypeId = eventType.Id,
                            Language = "en",
                            Name = kvp.Value
                        });
                        translationsAdded++;
                    }
                }
            }

            var drugMap = new Dictionary<string, string>
            {
                { "Paracetamol", "Paracetamol" },
                { "Ibuprofen", "Ibuprofen" },
                { "Aspirin", "Aspirin" },
                { "Antibiotika", "Antibiotics" },
                { "Inzulín", "Insulin" },
                { "fentanyl", "Fentanyl" }
            };

            foreach (var kvp in drugMap)
            {
                var drug = await _context.Drugs.FirstOrDefaultAsync(d => d.Name == kvp.Key);
                if (drug != null)
                {
                    var existingTranslation = await _context.DrugTranslations
                        .AnyAsync(dt => dt.DrugId == drug.Id && dt.Language == "en");
                    
                    if (!existingTranslation)
                    {
                        _context.DrugTranslations.Add(new DrugTranslation
                        {
                            DrugId = drug.Id,
                            Language = "en",
                            Name = kvp.Value
                        });
                        translationsAdded++;
                    }
                }
            }

            var examinationTypeMap = new Dictionary<string, string>
            {
                { "Krevní test", "Blood Test" },
                { "Rentgen", "X-Ray" },
                { "MRI", "MRI" },
                { "CT", "CT" },
                { "Ultrazvuk", "Ultrasound" },
                { "MEIK", "MEIK" }
            };

            foreach (var kvp in examinationTypeMap)
            {
                var examinationType = await _context.ExaminationTypes.FirstOrDefaultAsync(et => et.Name == kvp.Key);
                if (examinationType != null)
                {
                    var existingTranslation = await _context.ExaminationTypeTranslations
                        .AnyAsync(ett => ett.ExaminationTypeId == examinationType.Id && ett.Language == "en");
                    
                    if (!existingTranslation)
                    {
                        _context.ExaminationTypeTranslations.Add(new ExaminationTypeTranslation
                        {
                            ExaminationTypeId = examinationType.Id,
                            Language = "en",
                            Name = kvp.Value
                        });
                        translationsAdded++;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok($"Added {translationsAdded} translations.");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
