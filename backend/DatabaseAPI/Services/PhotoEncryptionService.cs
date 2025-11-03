using System.Security.Cryptography;

namespace DatabaseAPI.Services;

public class PhotoEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly ILogger<PhotoEncryptionService> _logger;

    public PhotoEncryptionService(IConfiguration configuration, ILogger<PhotoEncryptionService> logger)
    {
        _logger = logger;
        
        var keyString = configuration["PhotoEncryption:Key"] 
            ?? "DefaultKey1234567890123456789012";
        var ivString = configuration["PhotoEncryption:IV"] 
            ?? "DefaultIV12345678";
        
        _key = System.Text.Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        _iv = System.Text.Encoding.UTF8.GetBytes(ivString.PadRight(16).Substring(0, 16));
    }

    public async Task<byte[]> EncryptPhotoAsync(byte[] photoData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        await using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        {
            await csEncrypt.WriteAsync(photoData, 0, photoData.Length);
            await csEncrypt.FlushFinalBlockAsync();
        }

        return msEncrypt.ToArray();
    }

    public async Task<byte[]> DecryptPhotoAsync(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(encryptedData);
        await using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var msOutput = new MemoryStream();
        await csDecrypt.CopyToAsync(msOutput);
        return msOutput.ToArray();
    }
}
