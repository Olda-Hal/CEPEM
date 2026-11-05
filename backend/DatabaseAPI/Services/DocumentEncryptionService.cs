namespace DatabaseAPI.Services;

/// <summary>
/// Encrypts patient documents with authenticated AES-GCM (CEPM2) while keeping
/// legacy CEPM1/raw-CBC files readable during migration.
/// </summary>
public class DocumentEncryptionService
{
    private readonly AesFileCipher _cipher;

    public DocumentEncryptionService(IConfiguration configuration, ILogger<DocumentEncryptionService> logger)
    {
        var keyString = configuration["DocumentEncryption:Key"]
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__Key")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new InvalidOperationException(
                "Document encryption key is not configured. Set the DocumentEncryption__Key configuration value or the DOCUMENT_ENCRYPTION_KEY environment variable.");
        }

        // Legacy static IV - only required for decrypting files not yet re-encrypted.
        var ivString = configuration["DocumentEncryption:IV"]
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__IV")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_IV");

        _cipher = new AesFileCipher(keyString, ivString, "document");
    }

    public Task<byte[]> EncryptDocumentAsync(byte[] documentData) =>
        Task.FromResult(_cipher.Encrypt(documentData));

    public Task<byte[]> DecryptDocumentAsync(byte[] encryptedData) =>
        Task.FromResult(_cipher.Decrypt(encryptedData));

    /// <summary>True when the data is already in the authenticated CEPM2 format.</summary>
    public bool IsCurrentFormat(byte[] encryptedData) => _cipher.IsCurrentFormat(encryptedData);
}
