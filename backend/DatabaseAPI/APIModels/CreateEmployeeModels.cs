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

        /// <summary>
        /// Roles granted to the new account. When empty, the service assigns
        /// the baseline "Examiner" role so the account isn't permanently
        /// default-denied on every permission-gated endpoint (/me included).
        /// </summary>
        public List<int> RoleIds { get; set; } = new List<int>();
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
