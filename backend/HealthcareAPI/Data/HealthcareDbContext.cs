using Microsoft.EntityFrameworkCore;
using HealthcareAPI.Models;
using BCrypt.Net;

namespace HealthcareAPI.Data
{
    public class HealthcareDbContext : DbContext
    {
        public HealthcareDbContext(DbContextOptions<HealthcareDbContext> options) : base(options)
        {
        }

        public DbSet<Doctor> Doctors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Doctor entity configuration
            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.Salt).IsRequired();
                entity.Property(e => e.Specialization).HasMaxLength(150);
                entity.Property(e => e.LicenseNumber).HasMaxLength(50);
                
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.LicenseNumber).IsUnique();
            });
        }

        public async Task SeedDataAsync()
        {
            if (!Doctors.Any())
            {
                var defaultDoctors = new List<Doctor>
                {
                    CreateDoctor("Jan", "Novák", "jan.novak@cepem.cz", "password123", "Kardiologie", "KAR001"),
                    CreateDoctor("Marie", "Svobodová", "marie.svobodova@cepem.cz", "password123", "Neurologie", "NEU001"),
                    CreateDoctor("Petr", "Dvořák", "petr.dvorak@cepem.cz", "password123", "Ortopédie", "ORT001"),
                    CreateDoctor("Admin", "Administrator", "admin@cepem.cz", "admin123", "Administrátor", "ADM001")
                };

                Doctors.AddRange(defaultDoctors);
                await SaveChangesAsync();
            }
        }

        private Doctor CreateDoctor(string firstName, string lastName, string email, string password, string specialization, string licenseNumber)
        {
            var salt = BCrypt.Net.BCrypt.GenerateSalt(12);
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, salt);

            return new Doctor
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                PasswordHash = passwordHash,
                Salt = salt,
                Specialization = specialization,
                LicenseNumber = licenseNumber,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}