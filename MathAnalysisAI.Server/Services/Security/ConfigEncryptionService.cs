using System.Security.Cryptography;

namespace MathAnalysisAI.Server.Services.Security;

public static class ConfigEncryptionService
{
    private const string EncryptedValuePrefix = "ENC:";

    public static bool IsEncrypted(string? value)
    {
        return value != null && value.StartsWith(EncryptedValuePrefix, StringComparison.Ordinal);
    }

    public static string Decrypt(string base64Ciphertext, byte[] key)
    {
        var ciphertext = Convert.FromBase64String(base64Ciphertext);

        if (ciphertext.Length < 28)
            throw new CryptographicException("Invalid encrypted value: ciphertext too short.");

        var nonce = new byte[12];
        var tag = new byte[16];
        var encrypted = new byte[ciphertext.Length - 28];

        Buffer.BlockCopy(ciphertext, 0, nonce, 0, 12);
        Buffer.BlockCopy(ciphertext, ciphertext.Length - 16, tag, 0, 16);
        Buffer.BlockCopy(ciphertext, 12, encrypted, 0, encrypted.Length);

        var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }

    public static string Encrypt(string plaintext, byte[] key)
    {
        var nonce = new byte[12];
        var tag = new byte[16];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var result = new byte[12 + plaintextBytes.Length + 16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        Buffer.BlockCopy(nonce, 0, result, 0, 12);
        Buffer.BlockCopy(ciphertext, 0, result, 12, plaintextBytes.Length);
        Buffer.BlockCopy(tag, 0, result, 12 + plaintextBytes.Length, 16);

        return Convert.ToBase64String(result);
    }

    public static string EncryptToConfigValue(string plaintext, byte[] key)
    {
        return EncryptedValuePrefix + Encrypt(plaintext, key);
    }

    public static byte[]? LoadEncryptionKey()
    {
        var hexKey = Environment.GetEnvironmentVariable("MATHANALYSIS_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(hexKey))
            return null;

        hexKey = hexKey.Trim();
        if (hexKey.Length != 64)
            throw new InvalidOperationException(
                "MATHANALYSIS_ENCRYPTION_KEY must be a 64-character hex string (32 bytes / 256 bits).");

        return Convert.FromHexString(hexKey);
    }
}
