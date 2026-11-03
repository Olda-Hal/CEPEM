using System.Security.Cryptography;
using System.Text;

namespace DatabaseAPI.Services;

/// <summary>
/// Authenticated file encryption for patient photos/documents.
///
/// Current format (CEPM2):
///   magic "CEPM2" (5) + random nonce (12) + GCM tag (16) + ciphertext
/// The key is derived with HKDF-SHA256 from the configured secret, and the
/// associated data binds the file to its context (photo/document), so a
/// ciphertext cannot be silently modified or moved between stores.
///
/// Legacy formats remain readable:
///   - "CEPM1" + random IV (16) + AES-CBC ciphertext
///   - raw AES-CBC ciphertext encrypted with the static configured IV
/// The legacy key derivation (PadRight/Substring) is reproduced exactly.
/// </summary>
public sealed class AesFileCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int LegacyIvSize = 16;

    private static readonly byte[] CurrentMagic = "CEPM2"u8.ToArray();
    private static readonly byte[] LegacyMagic = "CEPM1"u8.ToArray();

    private readonly byte[] _currentKey;
    private readonly byte[] _legacyKey;
    private readonly byte[]? _legacyIv;
    private readonly byte[] _associatedData;

    public AesFileCipher(string keyString, string? legacyIvString, string context)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new ArgumentException("Encryption key is not configured.", nameof(keyString));
        }

        var keyMaterial = Encoding.UTF8.GetBytes(keyString);
        _associatedData = Encoding.UTF8.GetBytes($"CEPEM2:{context}");
        _currentKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            keyMaterial,
            KeySize,
            salt: Array.Empty<byte>(),
            info: _associatedData);

        // Exact legacy derivation, kept only so files written before CEPM2
        // remain decryptable.
        _legacyKey = Encoding.UTF8.GetBytes(keyString.PadRight(32).Substring(0, 32));
        _legacyIv = string.IsNullOrWhiteSpace(legacyIvString)
            ? null
            : Encoding.UTF8.GetBytes(
                legacyIvString.PadRight(LegacyIvSize).Substring(0, LegacyIvSize));
    }

    /// <summary>True when the data is already in the authenticated CEPM2 envelope.</summary>
    public bool IsCurrentFormat(ReadOnlySpan<byte> data) =>
        data.Length >= CurrentMagic.Length + NonceSize + TagSize &&
        data[..CurrentMagic.Length].SequenceEqual(CurrentMagic);

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_currentKey, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, _associatedData);

        var output = new byte[CurrentMagic.Length + NonceSize + TagSize + ciphertext.Length];
        var offset = 0;
        CurrentMagic.CopyTo(output, offset);
        offset += CurrentMagic.Length;
        nonce.CopyTo(output, offset);
        offset += NonceSize;
        tag.CopyTo(output, offset);
        offset += TagSize;
        ciphertext.CopyTo(output, offset);
        return output;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> encrypted)
    {
        if (IsCurrentFormat(encrypted))
        {
            return DecryptCurrent(encrypted);
        }

        // CEPM1: authenticated only by AES-CBC padding, retained for migration.
        if (encrypted.Length > LegacyMagic.Length + LegacyIvSize &&
            encrypted[..LegacyMagic.Length].SequenceEqual(LegacyMagic))
        {
            var iv = encrypted.Slice(LegacyMagic.Length, LegacyIvSize).ToArray();
            var ciphertext = encrypted[(LegacyMagic.Length + LegacyIvSize)..].ToArray();
            return DecryptLegacyCbc(ciphertext, iv);
        }

        if (_legacyIv == null)
        {
            throw new InvalidOperationException(
                "Encountered a legacy-format encrypted file but no legacy IV is configured. " +
                "Set the legacy IV or re-encrypt the stored files.");
        }

        return DecryptLegacyCbc(encrypted.ToArray(), _legacyIv);
    }

    private byte[] DecryptCurrent(ReadOnlySpan<byte> encrypted)
    {
        var nonce = encrypted.Slice(CurrentMagic.Length, NonceSize);
        var tag = encrypted.Slice(CurrentMagic.Length + NonceSize, TagSize);
        var ciphertext = encrypted[(CurrentMagic.Length + NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_currentKey, TagSize);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, _associatedData);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Encrypted file integrity check failed; the file was modified or is corrupt.", ex);
        }

        return plaintext;
    }

    private byte[] DecryptLegacyCbc(byte[] ciphertext, byte[] iv)
    {
        if (_legacyKey.Length is not (16 or 24 or KeySize))
        {
            throw new InvalidOperationException(
                "The configured legacy encryption key does not produce a valid AES key; " +
                "legacy files cannot be decrypted with it.");
        }

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _legacyKey;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var input = new MemoryStream(ciphertext);
        using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        crypto.CopyTo(output);
        return output.ToArray();
    }
}
