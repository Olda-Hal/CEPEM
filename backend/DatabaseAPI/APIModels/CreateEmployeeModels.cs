namespace DatabaseAPI.APIModels
{
    public class CreateEmployeeRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TitleBefore { get; set; }
        public string? TitleAfter { get; set; }
        public bool Active { get; set; } = true;
    }

    public class CreateEmployeeResponse
    {
        public int EmployeeId { get; set; }
        public int PersonId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UID { get; set; } = string.Empty;
        public bool Active { get; set; }
    }
}
