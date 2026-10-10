using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ScripOx.Desktop.Services
{
    public class LicenseData
    {
        public string LicenseKey { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    public static class LicenseService
    {
        private static readonly string RegistryPath = @"SOFTWARE\ScripOx\License";
        private static readonly string ApiUrl = "http://oxtech.uk/api/license"; // Placeholder Laravel API

        // Helper to get AES Key based on Hardware ID
        private static byte[] GetEncryptionKey()
        {
            string hwId = HardwareInfoService.GetHardwareId();
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(hwId + "ScripOxSecretSalt99"));
            }
        }

        public static void SaveLicense(LicenseData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                string encrypted = EncryptString(json, GetEncryptionKey());
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    key.SetValue("Data", encrypted);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving license: {ex.Message}");
            }
        }

        public static LicenseData? LoadLicense()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        string? encrypted = key.GetValue("Data") as string;
                        if (!string.IsNullOrEmpty(encrypted))
                        {
                            string json = DecryptString(encrypted, GetEncryptionKey());
                            return JsonSerializer.Deserialize<LicenseData>(json);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading license: {ex.Message}");
            }
            return null;
        }

        public static bool IsActivated()
        {
            var license = LoadLicense();
            if (license == null) return false;
            if (!license.IsActive) return false;
            if (license.ExpiresAt < DateTime.UtcNow) return false;

            return true;
        }

        public static async Task<(bool Success, string Message, DateTime? ExpiresAt)> ActivateOnlineAsync(string licenseKey)
        {
            string hwId = HardwareInfoService.GetHardwareId();

            try
            {
                using (var client = new HttpClient())
                {
                    // Simulated logic:
                    // var content = new StringContent(JsonSerializer.Serialize(new { license_key = licenseKey, hardware_id = hwId }), Encoding.UTF8, "application/json");
                    // var response = await client.PostAsync($"{ApiUrl}/activate", content);
                    // var result = JsonSerializer.Deserialize<ActivationResponse>(await response.Content.ReadAsStringAsync());
                    // ...

                    // DUMMY IMPLEMENTATION UNTIL LARAVEL IS READY:
                    await Task.Delay(1000); // Simulate network request
                    
                    if (licenseKey == "TEST-KEY-123" || licenseKey.StartsWith("OX-"))
                    {
                        var expiresAt = DateTime.UtcNow.AddYears(1);
                        SaveLicense(new LicenseData { LicenseKey = licenseKey, IsActive = true, ExpiresAt = expiresAt });
                        return (true, "License activated successfully!", expiresAt);
                    }
                    else
                    {
                        return (false, "Invalid license key.", null);
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}", null);
            }
        }

        private static string EncryptString(string text, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(text);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static string DecryptString(string cipherText, byte[] key)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            using (Aes aes = Aes.Create())
            {
                byte[] iv = new byte[aes.BlockSize / 8];
                byte[] cipher = new byte[fullCipher.Length - iv.Length];
                
                Array.Copy(fullCipher, iv, iv.Length);
                Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);
                
                aes.Key = key;
                aes.IV = iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                
                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
