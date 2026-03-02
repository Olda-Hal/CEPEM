using System.ComponentModel.DataAnnotations;

namespace DatabaseAPI.DatabaseModels;

public class Person
{
    public int Id { get; set; }
    
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public string LastName { get; set; } = string.Empty;
    
    public string? TitleBefore { get; set; }
    
    public string? TitleAfter { get; set; }
    
    [Required]
    public string UID { get; set; } = string.Empty;
    
    public bool Active { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public string Gender { get; set; } = string.Empty;
    
    public int? CommentId { get; set; }
    public Comment? Comment { get; set; }

    public int? AddressId { get; set; }
    public Address? Address { get; set; }

    // Navigation properties
    public ICollection<FirstNameHistory> FirstNameHistories { get; set; } = new List<FirstNameHistory>();
    public ICollection<LastNameHistory> LastNameHistories { get; set; } = new List<LastNameHistory>();
    public ICollection<EmailHistory> EmailHistories { get; set; } = new List<EmailHistory>();
    public ICollection<PhoneNumberHistory> PhoneNumberHistories { get; set; } = new List<PhoneNumberHistory>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<ContactToObject> ContactToObjects { get; set; } = new List<ContactToObject>();
    public Patient? Patient { get; set; }
    public Employee? Employee { get; set; }
}
