using System.Security.Cryptography;
using System.Text;

namespace DatabaseAPI.Services;

public class DocumentEncryptionService
{
    // New file format: "CEPM1" magic + 16-byte random IV + AES-CBC ciphertext.
    // Legacy files (no magic) are raw AES-CBC ciphertext encrypted with the
    // configured static IV; they are handled until the re-encryption migration completes.
    private static readonly byte[] MagicHeader = "CEPM1"u8.ToArray();

    private readonly byte[] _key;
    private readonly byte[]? _legacyIv;
    private readonly ILogger<DocumentEncryptionService> _logger;

    public DocumentEncryptionService(IConfiguration configuration, ILogger<DocumentEncryptionService> logger)
    {
        _logger = logger;

        var keyString = configuration["DocumentEncryption:Key"]
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__Key")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new InvalidOperationException(
                "Document encryption key is not configured. Set the DocumentEncryption__Key configuration value or the DOCUMENT_ENCRYPTION_KEY environment variable.");
        }

        // Legacy static IV - only required for decrypting files not yet re-encrypted
        var ivString = configuration["DocumentEncryption:IV"]
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__IV")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_IV");

        _key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        _legacyIv = string.IsNullOrWhiteSpace(ivString)
            ? null
            : Encoding.UTF8.GetBytes(ivString.PadRight(16).Substring(0, 16));
    }

    public async Task<byte[]> EncryptDocumentAsync(byte[] documentData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = RandomNumberGenerator.GetBytes(16);

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(MagicHeader, 0, MagicHeader.Length);
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            await csEncrypt.WriteAsync(documentData, 0, documentData.Length);
            csEncrypt.FlushFinalBlock();
        }

        return msEncrypt.ToArray();
    }

    public async Task<byte[]> DecryptDocumentAsync(byte[] encryptedData)
    {
        byte[] iv;
        byte[] ciphertext;

        if (encryptedData.Length > MagicHeader.Length + 16 &&
            encryptedData.AsSpan(0, MagicHeader.Length).SequenceEqual(MagicHeader))
        {
            // New format: per-file random IV embedded after the magic header
            iv = encryptedData[MagicHeader.Length..(MagicHeader.Length + 16)];
            ciphertext = encryptedData[(MagicHeader.Length + 16)..];
        }
        else
        {
            // Legacy format: static IV from configuration
            if (_legacyIv == null)
            {
                throw new InvalidOperationException(
                    "Encountered a legacy-format encrypted document but no legacy IV is configured (DocumentEncryption:IV). " +
                    "Set the legacy IV or re-encrypt the stored files.");
            }
            iv = _legacyIv;
            ciphertext = encryptedData;
        }

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(ciphertext);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msPlain = new MemoryStream();

        await csDecrypt.CopyToAsync(msPlain);

        return msPlain.ToArray();
    }
}
