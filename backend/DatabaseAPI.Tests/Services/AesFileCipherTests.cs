using System.Security.Cryptography;
using System.Text;
using DatabaseAPI.Services;
using Xunit;

namespace DatabaseAPI.Tests.Services;

public class AesFileCipherTests
{
    private const string Key = "test-encryption-key-32chars-long!";
    private const string LegacyIv = "1234567890123456";

    [Fact]
    public void EncryptDecrypt_RoundtripsAndUsesCepm2()
    {
        var cipher = new AesFileCipher(Key, LegacyIv, "photo");
        var plaintext = Encoding.UTF8.GetBytes("patient photo bytes");

        var encrypted = cipher.Encrypt(plaintext);

        Assert.True(cipher.IsCurrentFormat(encrypted));
        Assert.Equal("CEPM2", Encoding.ASCII.GetString(encrypted, 0, 5));
        Assert.Equal(plaintext, cipher.Decrypt(encrypted));
    }

    [Fact]
    public void TamperedCiphertext_IsRejected()
    {
        var cipher = new AesFileCipher(Key, LegacyIv, "photo");
        var encrypted = cipher.Encrypt(Encoding.UTF8.GetBytes("tamper me"));
        encrypted[^1] ^= 0xFF;

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(encrypted));
    }

    [Fact]
    public void TamperedTag_IsRejected()
    {
        var cipher = new AesFileCipher(Key, LegacyIv, "document");
        var encrypted = cipher.Encrypt(Encoding.UTF8.GetBytes("tag test"));
        // Tag starts after magic (5) + nonce (12).
        encrypted[5 + 12] ^= 0x01;

        Assert.Throws<CryptographicException>(() => cipher.Decrypt(encrypted));
    }

    [Fact]
    public void DifferentContext_IsRejected()
    {
        var photoCipher = new AesFileCipher(Key, LegacyIv, "photo");
        var documentCipher = new AesFileCipher(Key, LegacyIv, "document");
        var encrypted = photoCipher.Encrypt(Encoding.UTF8.GetBytes("context bound"));

        Assert.Throws<CryptographicException>(() => documentCipher.Decrypt(encrypted));
    }

    [Fact]
    public void LegacyCepm1Envelope_IsStillReadable()
    {
        var plaintext = Encoding.UTF8.GetBytes("legacy cepm1 file");
        // Reproduce the pre-CEPM2 key derivation exactly.
        var legacyKey = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
        var iv = Encoding.UTF8.GetBytes(LegacyIv);

        using var aes = Aes.Create();
        aes.Key = legacyKey;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var legacyFile = new byte[5 + iv.Length + ciphertext.Length];
        "CEPM1"u8.CopyTo(legacyFile);
        iv.CopyTo(legacyFile, 5);
        ciphertext.CopyTo(legacyFile, 5 + iv.Length);

        var cipher = new AesFileCipher(Key, LegacyIv, "document");
        Assert.Equal(plaintext, cipher.Decrypt(legacyFile));
    }

    [Fact]
    public void LegacyRawCbcFile_IsStillReadable()
    {
        var plaintext = Encoding.UTF8.GetBytes("raw legacy file");
        var legacyKey = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
        var iv = Encoding.UTF8.GetBytes(LegacyIv);

        using var aes = Aes.Create();
        aes.Key = legacyKey;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);

        var cipher = new AesFileCipher(Key, LegacyIv, "photo");
        Assert.Equal(plaintext, cipher.Decrypt(ciphertext));
    }
}
