namespace DatabaseAPI.DatabaseModels;

public class Contact
{
    public int Id { get; set; }

    // Navigation properties
    public ICollection<ContactPhoneNumber> PhoneNumbers { get; set; } = new List<ContactPhoneNumber>();
    public ICollection<ContactEmail> Emails { get; set; } = new List<ContactEmail>();
    public ICollection<ContactToObject> ContactToObjects { get; set; } = new List<ContactToObject>();
}
