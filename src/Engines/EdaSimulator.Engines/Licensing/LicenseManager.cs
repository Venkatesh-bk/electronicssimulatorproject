using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EdaSimulator.Engines.Licensing
{
    public enum LicenseTier
    {
        Community,
        Professional,
        Enterprise
    }

    public class LicenseInfo
    {
        public string RegisteredTo { get; set; } = "Community User";
        public LicenseTier Tier { get; set; } = LicenseTier.Community;
        public DateTime ExpiryDate { get; set; } = DateTime.MaxValue;
        public bool IsValid { get; set; } = true;
    }

    public static class LicenseManager
    {
        private static readonly string LicenseFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EdaSimulator", "license.dat");

        public static LicenseInfo CurrentLicense { get; private set; } = new LicenseInfo();

        public static void Initialize()
        {
            if (File.Exists(LicenseFilePath))
            {
                try
                {
                    string json = File.ReadAllText(LicenseFilePath);
                    var license = JsonSerializer.Deserialize<LicenseInfo>(json);
                    
                    if (license != null && license.ExpiryDate > DateTime.Now)
                    {
                        CurrentLicense = license;
                        CurrentLicense.IsValid = true;
                    }
                    else
                    {
                        CurrentLicense = new LicenseInfo { IsValid = false }; // Expired
                    }
                }
                catch
                {
                    // Invalid format, default to Community
                    CurrentLicense = new LicenseInfo();
                }
            }
        }

        public static bool ActivateLicense(string licenseKey)
        {
            // Mock validation algorithm
            // Professional keys start with "PRO-"
            // Enterprise keys start with "ENT-"
            // Example: PRO-1234-ABCD-5678
            
            if (string.IsNullOrWhiteSpace(licenseKey)) return false;

            licenseKey = licenseKey.Trim().ToUpper();
            
            if (licenseKey.StartsWith("PRO-") && licenseKey.Length >= 15)
            {
                CurrentLicense = new LicenseInfo
                {
                    RegisteredTo = "Professional User",
                    Tier = LicenseTier.Professional,
                    ExpiryDate = DateTime.Now.AddYears(1),
                    IsValid = true
                };
                SaveLicense();
                return true;
            }
            else if (licenseKey.StartsWith("ENT-") && licenseKey.Length >= 15)
            {
                CurrentLicense = new LicenseInfo
                {
                    RegisteredTo = "Enterprise User",
                    Tier = LicenseTier.Enterprise,
                    ExpiryDate = DateTime.Now.AddYears(1),
                    IsValid = true
                };
                SaveLicense();
                return true;
            }

            return false;
        }

        private static void SaveLicense()
        {
            try
            {
                string dir = Path.GetDirectoryName(LicenseFilePath)!;
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(CurrentLicense);
                File.WriteAllText(LicenseFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
