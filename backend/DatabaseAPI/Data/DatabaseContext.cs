using Microsoft.EntityFrameworkCore;
using DatabaseAPI.DatabaseModels;

namespace DatabaseAPI.Data;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
    }

    // Core entities
    public DbSet<Person> Persons { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    
    // Event related entities
    public DbSet<Event> Events { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    
    // Drug related entities
    public DbSet<Drug> Drugs { get; set; }
    public DbSet<DrugCategory> DrugCategories { get; set; }
    public DbSet<DrugToDrugCategory> DrugToDrugCategories { get; set; }
    public DbSet<DrugUse> DrugUses { get; set; }
    
    // Medical entities
    public DbSet<ExaminationType> ExaminationTypes { get; set; }
    public DbSet<Examination> Examinations { get; set; }
    public DbSet<Symptom> Symptoms { get; set; }
    public DbSet<PatientSymptom> PatientSymptoms { get; set; }
    public DbSet<Pregnancy> Pregnancies { get; set; }
    public DbSet<InjuryType> InjuryTypes { get; set; }
    public DbSet<Injury> Injuries { get; set; }
    public DbSet<VaccineType> VaccineTypes { get; set; }
    public DbSet<Vaccine> Vaccines { get; set; }
    
    // History entities
    public DbSet<FirstNameHistory> FirstNameHistories { get; set; }
    public DbSet<LastNameHistory> LastNameHistories { get; set; }
    public DbSet<EmailHistory> EmailHistories { get; set; }
    public DbSet<PhoneNumberHistory> PhoneNumberHistories { get; set; }
    
    // Hospital entities
    public DbSet<Hospital> Hospitals { get; set; }
    public DbSet<Equipment> Equipment { get; set; }
    public DbSet<HospitalEquipment> HospitalEquipment { get; set; }
    public DbSet<HospitalEmployee> HospitalEmployees { get; set; }
    public DbSet<HospitalExaminationType> HospitalExaminationTypes { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    
    // System entities
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    
    // Translation entities
    public DbSet<Translation> Translations { get; set; }

    // Form submission entities
    public DbSet<FormSubmission> FormSubmissions { get; set; }
    public DbSet<FormSubmissionMedication> FormSubmissionMedications { get; set; }
    public DbSet<FormSubmissionLifestyle> FormSubmissionLifestyles { get; set; }
    public DbSet<FormSubmissionReproductiveHealth> FormSubmissionReproductiveHealths { get; set; }
    public DbSet<FormSubmissionConsent> FormSubmissionConsents { get; set; }
    public DbSet<SicknessHistory> SicknessHistories { get; set; }

    // Contact entities
    public DbSet<Contact> Contacts { get; set; }
    public DbSet<ContactPhoneNumber> ContactPhoneNumbers { get; set; }
    public DbSet<ContactEmail> ContactEmails { get; set; }
    public DbSet<ContactToObject> ContactToObjects { get; set; }

    // Address entities
    public DbSet<Address> Addresses { get; set; }

    // Document entities
    public DbSet<PatientDocument> PatientDocuments { get; set; }
    public DbSet<ExaminationDocument> ExaminationDocuments { get; set; }
    
    // Reservation related entities
    public DbSet<ExaminationRoom> ExaminationRooms { get; set; }
    public DbSet<DoctorExaminationRoom> DoctorExaminationRooms { get; set; }
    public DbSet<Reservation> Reservations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure relationships and constraints
        ConfigurePersonRelationships(modelBuilder);
        ConfigureEventRelationships(modelBuilder);
        ConfigureDrugRelationships(modelBuilder);
        ConfigureMedicalRelationships(modelBuilder);
        ConfigureHistoryRelationships(modelBuilder);
        ConfigureHospitalRelationships(modelBuilder);
        ConfigureReservationRelationships(modelBuilder);
        ConfigureContactRelationships(modelBuilder);
        ConfigureAddressRelationships(modelBuilder);
        ConfigureTranslationRelationships(modelBuilder);
        ConfigureFormSubmissionRelationships(modelBuilder);
    }
    
    private void ConfigurePersonRelationships(ModelBuilder modelBuilder)
    {
        // Person -> Patient (1:0..1)
        modelBuilder.Entity<Patient>()
            .HasOne(p => p.Person)
            .WithOne(p => p.Patient)
            .HasForeignKey<Patient>(p => p.PersonId);
            
        // Person -> Employee (1:0..1)
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Person)
            .WithOne(p => p.Employee)
            .HasForeignKey<Employee>(e => e.PersonId);
            
        // Person -> Comment (optional)
        modelBuilder.Entity<Person>()
            .HasOne(p => p.Comment)
            .WithMany(c => c.Persons)
            .HasForeignKey(p => p.CommentId);
            
        // Patient -> Comment (optional)
        modelBuilder.Entity<Patient>()
            .HasOne(p => p.Comment)
            .WithMany(c => c.Patients)
            .HasForeignKey(p => p.CommentId);
            
        // Patient -> PatientDocument (1:many)
        modelBuilder.Entity<PatientDocument>()
            .HasOne(pd => pd.Patient)
            .WithMany(p => p.Documents)
            .HasForeignKey(pd => pd.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
    
    private void ConfigureEventRelationships(ModelBuilder modelBuilder)
    {
        // Event -> Patient
        modelBuilder.Entity<Event>()
            .HasOne(e => e.Patient)
            .WithMany(p => p.Events)
            .HasForeignKey(e => e.PatientId);
            
        // Event -> EventType
        modelBuilder.Entity<Event>()
            .HasOne(e => e.EventType)
            .WithMany(et => et.Events)
            .HasForeignKey(e => e.EventTypeId);
            
        // Event -> Comment (optional)
        modelBuilder.Entity<Event>()
            .HasOne(e => e.Comment)
            .WithMany(c => c.Events)
            .HasForeignKey(e => e.CommentId);
    }
    
    private void ConfigureDrugRelationships(ModelBuilder modelBuilder)
    {
        // DrugUse relationships
        modelBuilder.Entity<DrugUse>()
            .HasOne(du => du.Drug)
            .WithMany(d => d.DrugUses)
            .HasForeignKey(du => du.DrugId);
            
        modelBuilder.Entity<DrugUse>()
            .HasOne(du => du.Event)
            .WithMany(e => e.DrugUses)
            .HasForeignKey(du => du.EventId);
            
        // DrugTranslation relationships
        modelBuilder.Entity<DrugToDrugCategory>()
            .HasOne(dtdc => dtdc.Drug)
            .WithMany(d => d.DrugToDrugCategories)
            .HasForeignKey(dtdc => dtdc.DrugId);
            
        modelBuilder.Entity<DrugToDrugCategory>()
            .HasOne(dtdc => dtdc.Category)
            .WithMany(dc => dc.DrugToDrugCategories)
            .HasForeignKey(dtdc => dtdc.CategoryId);
    }
    
    private void ConfigureMedicalRelationships(ModelBuilder modelBuilder)
    {
        // ExaminationTypeTranslation relationships
        modelBuilder.Entity<Examination>()
            .HasOne(e => e.ExaminationType)
            .WithMany(et => et.Examinations)
            .HasForeignKey(e => e.ExaminationTypeId);
            
        modelBuilder.Entity<Examination>()
            .HasOne(e => e.Event)
            .WithMany(ev => ev.Examinations)
            .HasForeignKey(e => e.EventId);

        // ExaminationDocument relationships
        modelBuilder.Entity<ExaminationDocument>()
            .HasOne(ed => ed.Examination)
            .WithMany(e => e.Documents)
            .HasForeignKey(ed => ed.ExaminationId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // PatientSymptom relationships
        modelBuilder.Entity<PatientSymptom>()
            .HasOne(ps => ps.Symptom)
            .WithMany(s => s.PatientSymptoms)
            .HasForeignKey(ps => ps.SymptomId);
            
        modelBuilder.Entity<PatientSymptom>()
            .HasOne(ps => ps.Event)
            .WithMany(e => e.PatientSymptoms)
            .HasForeignKey(ps => ps.EventId);
            
        // Pregnancy relationships
        modelBuilder.Entity<Pregnancy>()
            .HasOne(p => p.Event)
            .WithMany(e => e.Pregnancies)
            .HasForeignKey(p => p.EventId);
            
        // Injury relationships
        modelBuilder.Entity<Injury>()
            .HasOne(i => i.InjuryType)
            .WithMany(it => it.Injuries)
            .HasForeignKey(i => i.InjuryTypeId);
            
        modelBuilder.Entity<Injury>()
            .HasOne(i => i.Event)
            .WithMany(e => e.Injuries)
            .HasForeignKey(i => i.EventId);
            
        // Vaccine relationships
        modelBuilder.Entity<Vaccine>()
            .HasOne(v => v.VaccineType)
            .WithMany(vt => vt.Vaccines)
            .HasForeignKey(v => v.VaccineTypeId);
            
        modelBuilder.Entity<Vaccine>()
            .HasOne(v => v.Event)
            .WithMany(e => e.Vaccines)
            .HasForeignKey(v => v.EventId);
    }
    
    private void ConfigureHistoryRelationships(ModelBuilder modelBuilder)
    {
        // History relationships to Person
        modelBuilder.Entity<FirstNameHistory>()
            .HasOne(fnh => fnh.Person)
            .WithMany(p => p.FirstNameHistories)
            .HasForeignKey(fnh => fnh.PersonId);
            
        modelBuilder.Entity<LastNameHistory>()
            .HasOne(lnh => lnh.Person)
            .WithMany(p => p.LastNameHistories)
            .HasForeignKey(lnh => lnh.PersonId);
            
        modelBuilder.Entity<EmailHistory>()
            .HasOne(eh => eh.Person)
            .WithMany(p => p.EmailHistories)
            .HasForeignKey(eh => eh.PersonId);
            
        modelBuilder.Entity<PhoneNumberHistory>()
            .HasOne(pnh => pnh.Person)
            .WithMany(p => p.PhoneNumberHistories)
            .HasForeignKey(pnh => pnh.PersonId);
    }
    
    private void ConfigureHospitalRelationships(ModelBuilder modelBuilder)
    {
        // Hospital self-reference (parent/child)
        modelBuilder.Entity<Hospital>()
            .HasOne(h => h.ParentHospital)
            .WithMany(h => h.ChildHospitals)
            .HasForeignKey(h => h.ParentHospitalId)
            .OnDelete(DeleteBehavior.Restrict);

        // HospitalEquipment relationships
        modelBuilder.Entity<HospitalEquipment>()
            .HasOne(he => he.Hospital)
            .WithMany(h => h.HospitalEquipments)
            .HasForeignKey(he => he.HospitalId);
            
        modelBuilder.Entity<HospitalEquipment>()
            .HasOne(he => he.Equipment)
            .WithMany(e => e.HospitalEquipments)
            .HasForeignKey(he => he.EquipmentId);
            
        // HospitalEmployee relationships
        modelBuilder.Entity<HospitalEmployee>()
            .HasOne(he => he.Hospital)
            .WithMany(h => h.HospitalEmployees)
            .HasForeignKey(he => he.HospitalId);
            
        modelBuilder.Entity<HospitalEmployee>()
            .HasOne(he => he.Employee)
            .WithMany(e => e.HospitalEmployees)
            .HasForeignKey(he => he.EmployeeId);
            
        // Appointment relationships
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Person)
            .WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PersonId);
            
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.HospitalEmployee)
            .WithMany(he => he.Appointments)
            .HasForeignKey(a => a.EmployeeId);
            
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Equipment)
            .WithMany(e => e.Appointments)
            .HasForeignKey(a => a.EquipmentId);
            
        modelBuilder.Entity<Appointment>()
            .HasOne(a => a.Hospital)
            .WithMany(h => h.Appointments)
            .HasForeignKey(a => a.HospitalId);
            
        // HospitalExaminationType relationships
        modelBuilder.Entity<HospitalExaminationType>()
            .HasOne(het => het.Hospital)
            .WithMany(h => h.HospitalExaminationTypes)
            .HasForeignKey(het => het.HospitalId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HospitalExaminationType>()
            .HasOne(het => het.ExaminationType)
            .WithMany(et => et.HospitalExaminationTypes)
            .HasForeignKey(het => het.ExaminationTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HospitalExaminationType>()
            .HasIndex(het => new { het.HospitalId, het.ExaminationTypeId })
            .IsUnique();

        // UserRole relationships
        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(ur => ur.UserId);
            
        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);
    }
    
    private void ConfigureReservationRelationships(ModelBuilder modelBuilder)
    {
        // ExaminationRoom relationships
        modelBuilder.Entity<ExaminationRoom>()
            .HasMany(er => er.DoctorExaminationRooms)
            .WithOne(der => der.ExaminationRoom)
            .HasForeignKey(der => der.ExaminationRoomId);
            
        modelBuilder.Entity<ExaminationRoom>()
            .HasMany(er => er.Reservations)
            .WithOne(r => r.ExaminationRoom)
            .HasForeignKey(r => r.ExaminationRoomId);
            
        // DoctorExaminationRoom relationships
        modelBuilder.Entity<DoctorExaminationRoom>()
            .HasOne(der => der.Doctor)
            .WithMany(e => e.ExaminationRooms)
            .HasForeignKey(der => der.DoctorId);
            
        // Reservation relationships
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Doctor)
            .WithMany(e => e.Reservations)
            .HasForeignKey(r => r.DoctorId);
            
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Patient)
            .WithMany(p => p.Reservations)
            .HasForeignKey(r => r.PatientId);
            
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.ExaminationType)
            .WithMany(et => et.Reservations)
            .HasForeignKey(r => r.ExaminationTypeId);
    }

    private void ConfigureContactRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContactPhoneNumber>()
            .HasOne(cpn => cpn.Contact)
            .WithMany(c => c.PhoneNumbers)
            .HasForeignKey(cpn => cpn.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContactEmail>()
            .HasOne(ce => ce.Contact)
            .WithMany(c => c.Emails)
            .HasForeignKey(ce => ce.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContactToObject>()
            .HasOne(cto => cto.Contact)
            .WithMany(c => c.ContactToObjects)
            .HasForeignKey(cto => cto.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContactToObject>()
            .HasIndex(cto => new { cto.ObjectId, cto.ObjectType, cto.ContactId })
            .IsUnique();
    }

    private void ConfigureAddressRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>()
            .HasOne(p => p.Address)
            .WithOne(a => a.Person)
            .HasForeignKey<Person>(p => p.AddressId);

        modelBuilder.Entity<Hospital>()
            .HasOne(h => h.Address)
            .WithOne(a => a.Hospital)
            .HasForeignKey<Hospital>(h => h.AddressId);
    }

    private void ConfigureTranslationRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExaminationType>()
            .HasOne(e => e.NameTranslation)
            .WithMany()
            .HasForeignKey(e => e.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<EventType>()
            .HasOne(e => e.NameTranslation)
            .WithMany()
            .HasForeignKey(e => e.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Drug>()
            .HasOne(d => d.NameTranslation)
            .WithMany()
            .HasForeignKey(d => d.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DrugCategory>()
            .HasOne(dc => dc.NameTranslation)
            .WithMany()
            .HasForeignKey(dc => dc.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Symptom>()
            .HasOne(s => s.NameTranslation)
            .WithMany()
            .HasForeignKey(s => s.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<InjuryType>()
            .HasOne(i => i.NameTranslation)
            .WithMany()
            .HasForeignKey(i => i.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VaccineType>()
            .HasOne(v => v.NameTranslation)
            .WithMany()
            .HasForeignKey(v => v.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Role>()
            .HasOne(r => r.NameTranslation)
            .WithMany()
            .HasForeignKey(r => r.NameTranslationId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureFormSubmissionRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FormSubmission>()
            .HasOne(fs => fs.Patient)
            .WithMany(p => p.FormSubmissions)
            .HasForeignKey(fs => fs.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormSubmission>()
            .HasOne(fs => fs.Event)
            .WithMany(e => e.FormSubmissions)
            .HasForeignKey(fs => fs.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormSubmissionMedication>()
            .HasOne(fsm => fsm.FormSubmission)
            .WithOne(fs => fs.Medication)
            .HasForeignKey<FormSubmissionMedication>(fsm => fsm.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormSubmissionLifestyle>()
            .HasOne(fsl => fsl.FormSubmission)
            .WithOne(fs => fs.Lifestyle)
            .HasForeignKey<FormSubmissionLifestyle>(fsl => fsl.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormSubmissionReproductiveHealth>()
            .HasOne(fsr => fsr.FormSubmission)
            .WithOne(fs => fs.ReproductiveHealth)
            .HasForeignKey<FormSubmissionReproductiveHealth>(fsr => fsr.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FormSubmissionConsent>()
            .HasOne(fsc => fsc.FormSubmission)
            .WithOne(fs => fs.Consent)
            .HasForeignKey<FormSubmissionConsent>(fsc => fsc.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SicknessHistory>()
            .HasOne(sh => sh.FormSubmission)
            .WithMany(fs => fs.SicknessHistories)
            .HasForeignKey(sh => sh.FormSubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    }
