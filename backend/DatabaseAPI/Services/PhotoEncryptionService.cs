namespace DatabaseAPI.Services;

/// <summary>
/// Encrypts patient photos with authenticated AES-GCM (CEPM2) while keeping
/// legacy CEPM1/raw-CBC files readable during migration.
/// </summary>
public class PhotoEncryptionService
{
    private readonly AesFileCipher _cipher;

    public PhotoEncryptionService(IConfiguration configuration, ILogger<PhotoEncryptionService> logger)
    {
        var keyString = configuration["PhotoEncryption:Key"]
            ?? Environment.GetEnvironmentVariable("PhotoEncryption__Key")
            ?? Environment.GetEnvironmentVariable("PHOTO_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new InvalidOperationException(
                "Photo encryption key is not configured. Set the PhotoEncryption__Key configuration value or the PHOTO_ENCRYPTION_KEY environment variable.");
        }

        // Legacy static IV - only required for decrypting files not yet re-encrypted.
        var ivString = configuration["PhotoEncryption:IV"]
            ?? Environment.GetEnvironmentVariable("PhotoEncryption__IV")
            ?? Environment.GetEnvironmentVariable("PHOTO_ENCRYPTION_IV");

        _cipher = new AesFileCipher(keyString, ivString, "photo");
    }

    public Task<byte[]> EncryptPhotoAsync(byte[] photoData) =>
        Task.FromResult(_cipher.Encrypt(photoData));

    public Task<byte[]> DecryptPhotoAsync(byte[] encryptedData) =>
        Task.FromResult(_cipher.Decrypt(encryptedData));

    /// <summary>True when the data is already in the authenticated CEPM2 format.</summary>
    public bool IsCurrentFormat(byte[] encryptedData) => _cipher.IsCurrentFormat(encryptedData);
}
