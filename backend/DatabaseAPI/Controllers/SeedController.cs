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

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
