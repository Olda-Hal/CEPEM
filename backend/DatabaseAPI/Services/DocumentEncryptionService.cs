using System.Security.Cryptography;
using System.Text;

namespace DatabaseAPI.Services;

public class DocumentEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly ILogger<DocumentEncryptionService> _logger;

    public DocumentEncryptionService(IConfiguration configuration, ILogger<DocumentEncryptionService> logger)
    {
        _logger = logger;

        var keyString = configuration["DocumentEncryption:Key"] 
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__Key")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_KEY")
            ?? "3ecd03d810e78227081299f6540681f94e01e210ef56dc6b0491264e5860c349";
        var ivString = configuration["DocumentEncryption:IV"] 
            ?? Environment.GetEnvironmentVariable("DocumentEncryption__IV")
            ?? Environment.GetEnvironmentVariable("DOCUMENT_ENCRYPTION_IV")
            ?? "DefaultIV12345678";

        _key = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        _iv = Encoding.UTF8.GetBytes(ivString.PadRight(16).Substring(0, 16));
    }

    public async Task<byte[]> EncryptDocumentAsync(byte[] documentData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
        
        await csEncrypt.WriteAsync(documentData, 0, documentData.Length);
        csEncrypt.FlushFinalBlock();

        return msEncrypt.ToArray();
    }

    public async Task<byte[]> DecryptDocumentAsync(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(encryptedData);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msPlain = new MemoryStream();
        
        await csDecrypt.CopyToAsync(msPlain);

        return msPlain.ToArray();
    }
}
