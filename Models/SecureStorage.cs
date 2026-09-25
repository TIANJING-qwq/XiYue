using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SBtools.Models;

public static class SecureStorage
{
    private static readonly byte[] Key;
    private static readonly byte[] IV = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    static SecureStorage()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolBusytools");
        Directory.CreateDirectory(dir);
        var keyFile = Path.Combine(dir, "secure.key");

        if (File.Exists(keyFile))
        {
            Key = File.ReadAllBytes(keyFile);
            // AES 需要 16/24/32 字节，如果长度不对就重新生成
            if (Key.Length != 16 && Key.Length != 24 && Key.Length != 32)
            {
                using var aesNew = Aes.Create();
                aesNew.GenerateKey();
                Key = aesNew.Key;
                File.WriteAllBytes(keyFile, Key);
            }
        }
        else
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            Key = aes.Key;
            File.WriteAllBytes(keyFile, Key);
        }
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        var enc = aes.CreateEncryptor();
        var data = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(enc.TransformFinalBlock(data, 0, data.Length));
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            var dec = aes.CreateDecryptor();
            var data = Convert.FromBase64String(cipherText);
            return Encoding.UTF8.GetString(dec.TransformFinalBlock(data, 0, data.Length));
        }
        catch
        {
            return "";
        }
    }
}