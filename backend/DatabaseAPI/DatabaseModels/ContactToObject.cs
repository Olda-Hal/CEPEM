using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseAPI.DatabaseModels;

public enum ContactObjectType
{
    Person,
    Hospital
}

public class ContactToObject
{
    public int Id { get; set; }

    [Required]
    public int ContactId { get; set; }
    public Contact Contact { get; set; } = null!;

    [Required]
    public int ObjectId { get; set; }

    [Required]
    public ContactObjectType ObjectType { get; set; }

    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? HospitalId { get; set; }
    public Hospital? Hospital { get; set; }
}
