using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SBtools.Models;

public static class SecureStorage
{
    private static readonly byte[] Key;
    private static readonly byte[] IV = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };

    static SecureStorage()
    {
        var keyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolBusytools", "secure.key");
        if (File.Exists(keyFile))
            Key = File.ReadAllBytes(keyFile);
        else
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            Key = aes.Key;
            Directory.CreateDirectory(Path.GetDirectoryName(keyFile)!);
            File.WriteAllBytes(keyFile, Key);
        }
    }

    public static string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    public static string Decrypt(string cipherText)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch { return ""; }
    }
}