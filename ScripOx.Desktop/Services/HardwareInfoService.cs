using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace ScripOx.Desktop.Services
{
    public static class HardwareInfoService
    {
        public static string GetHardwareId()
        {
            string cpuInfo = GetCpuId();
            string diskInfo = GetDiskId();
            string rawId = $"{cpuInfo}-{diskInfo}";
            
            // Hash the combined string to make a clean, unique ID
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("X2"));
                }
                return builder.ToString();
            }
        }

        private static string GetCpuId()
        {
            try
            {
                string cpuInfo = string.Empty;
                using (ManagementClass mc = new ManagementClass("win32_processor"))
                using (ManagementObjectCollection moc = mc.GetInstances())
                {
                    foreach (ManagementObject mo in moc)
                    {
                        if (cpuInfo == "")
                        {
                            cpuInfo = mo.Properties["processorID"].Value?.ToString();
                            break;
                        }
                    }
                }
                return cpuInfo ?? "UNKNOWN_CPU";
            }
            catch
            {
                return "UNKNOWN_CPU";
            }
        }

        private static string GetDiskId()
        {
            try
            {
                string drive = string.Empty;
                using (ManagementObject dsk = new ManagementObject(@"win32_logicaldisk.deviceid=""c:"""))
                {
                    dsk.Get();
                    drive = dsk["VolumeSerialNumber"]?.ToString();
                }
                return drive ?? "UNKNOWN_DISK";
            }
            catch
            {
                return "UNKNOWN_DISK";
            }
        }
    }
}
