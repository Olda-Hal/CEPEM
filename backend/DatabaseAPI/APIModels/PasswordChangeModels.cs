namespace DatabaseAPI.APIModels
{
    public enum PasswordChangeResult
    {
        Success,
        InvalidCurrentPassword,
        SamePassword,
        ValidationError
    }

    public class PasswordChangeResponse
    {
        public PasswordChangeResult Result { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
