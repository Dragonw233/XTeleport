using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Teleport
{
    internal class MachineCodeGenerator
    {
        private static readonly HashSet<string> InvalidHardwareValues = new(StringComparer.Ordinal)
        {
            "0",
            "00",
            "00000000",
            "0000000000000000",
            "00000000000000000000000000000000",
            "DEFAULT",
            "DEFAULTSTRING",
            "NONE",
            "NA",
            "UNKNOWN",
            "SERIALNUMBER",
            "SYSTEMSERIALNUMBER",
            "TOBEFILLEDBYOEM",
            "FFFFFFFFFFFFFFFF",
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
            "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"
        };

        private static string GetHash(string value) => GetHexString(MD5.HashData(Encoding.ASCII.GetBytes(value)));

        private static string GetHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes)
            {
                builder.Append(item.ToString("X2"));
            }

            return builder.ToString();
        }

        private static string NormalizeHardwareValue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (InvalidHardwareValues.Contains(normalized))
            {
                return string.Empty;
            }

            if (normalized.All(ch => ch == '0') || normalized.All(ch => ch == 'F'))
            {
                return string.Empty;
            }

            return normalized;
        }

        private static List<string> QueryWmiValues(string query, params string[] propertyNames)
        {
            var values = new List<string>();

            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                using var results = searcher.Get();
                foreach (ManagementObject result in results)
                {
                    foreach (var propertyName in propertyNames)
                    {
                        var normalized = NormalizeHardwareValue(result[propertyName]?.ToString());
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            values.Add(normalized);
                        }
                    }
                }
            }
            catch
            {
                // Higher-level fallbacks decide whether enough hardware information was collected.
            }

            return values;
        }

        private static List<string> QueryRawWmiValues(string query, params string[] propertyNames)
        {
            var values = new List<string>();

            try
            {
                using var searcher = new ManagementObjectSearcher(query);
                using var results = searcher.Get();
                foreach (ManagementObject result in results)
                {
                    foreach (var propertyName in propertyNames)
                    {
                        var value = result[propertyName]?.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            values.Add(value);
                        }
                    }
                }
            }
            catch
            {
                // Higher-level fallbacks decide whether enough hardware information was collected.
            }

            return values;
        }

        private static string BuildComponentFingerprint(string componentName, IEnumerable<string> candidates)
        {
            var values = candidates
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            if (values.Length == 0)
            {
                return string.Empty;
            }

            return $"{componentName}:{GetHash(string.Join("|", values))}";
        }

        private static string BuildMachineCode(params string[] componentFingerprints)
        {
            var parts = componentFingerprints
                .Where(value => !string.IsNullOrEmpty(value))
                .ToArray();

            if (parts.Length == 0)
            {
                throw new Exception("Unable to fetch hardware information");
            }

            return GetHash(string.Join("|", parts));
        }

        private static string GetSystemDriveLetter()
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (string.IsNullOrWhiteSpace(systemRoot))
            {
                return "C:";
            }

            var root = Path.GetPathRoot(systemRoot);
            return string.IsNullOrWhiteSpace(root) ? "C:" : root.TrimEnd('\\');
        }

        private static IEnumerable<string> GetCpuCandidates()
        {
            return QueryWmiValues(
                "SELECT ProcessorId, UniqueId, Name FROM Win32_Processor",
                "ProcessorId",
                "UniqueId",
                "Name");
        }

        private static IEnumerable<string> GetSystemDiskCandidates()
        {
            var candidates = new List<string>();
            var systemDrive = GetSystemDriveLetter();

            candidates.AddRange(QueryWmiValues(
                $"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='{systemDrive}'",
                "VolumeSerialNumber"));

            var partitionIds = QueryRawWmiValues(
                $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{systemDrive}'}} WHERE AssocClass=Win32_LogicalDiskToPartition",
                "DeviceID");

            foreach (var partitionId in partitionIds.Distinct(StringComparer.Ordinal))
            {
                candidates.AddRange(QueryWmiValues(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partitionId}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition",
                    "SerialNumber",
                    "Signature",
                    "PNPDeviceID",
                    "Model"));
            }

            if (candidates.Count == 0)
            {
                candidates.AddRange(QueryWmiValues(
                    "SELECT SerialNumber, Signature, PNPDeviceID, Model FROM Win32_DiskDrive",
                    "SerialNumber",
                    "Signature",
                    "PNPDeviceID",
                    "Model"));
            }

            return candidates;
        }

        private static IEnumerable<string> GetBiosCandidates()
        {
            return QueryWmiValues(
                "SELECT SerialNumber, SMBIOSBIOSVersion, Manufacturer FROM Win32_BIOS",
                "SerialNumber",
                "SMBIOSBIOSVersion",
                "Manufacturer");
        }

        private static IEnumerable<string> GetBaseBoardCandidates()
        {
            return QueryWmiValues(
                "SELECT SerialNumber, Product, Manufacturer FROM Win32_BaseBoard",
                "SerialNumber",
                "Product",
                "Manufacturer");
        }

        private static IEnumerable<string> GetSystemProductCandidates()
        {
            return QueryWmiValues(
                "SELECT UUID, IdentifyingNumber, Vendor, Name FROM Win32_ComputerSystemProduct",
                "UUID",
                "IdentifyingNumber",
                "Vendor",
                "Name");
        }

        internal static string GenerateMachineCode()
        {
            return "闲鱼小店死个妈的";
        }

        internal static string GenerateMachineCodeV2()
        {
            var cpuId = BuildComponentFingerprint("CPU", GetCpuCandidates());
            var diskId = BuildComponentFingerprint("DISK", GetSystemDiskCandidates());
            var biosId = BuildComponentFingerprint("BIOS", GetBiosCandidates());
            var boardId = BuildComponentFingerprint("BOARD", GetBaseBoardCandidates());
            var systemId = BuildComponentFingerprint("SYSTEM", GetSystemProductCandidates());

            if (string.IsNullOrEmpty(cpuId) || string.IsNullOrEmpty(diskId))
            {
                throw new Exception("Unable to fetch hardware information");
            }

            return BuildMachineCode(cpuId, diskId, biosId, boardId, systemId);
        }

        private static readonly string saltA = "XiaoDianNiMaSiLe";
        private static readonly string saltB = "XTeleportProMax";

        internal static string GenerateActivationCode(string machineCode, int type)
        {
            var salt = "";
            switch (type)
            {
                case 1:
                    salt = saltA;
                    break;
                case 2:
                    salt = saltB;
                    break;
            }

            var saltedMachineCode = machineCode + salt;
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedMachineCode));

            var builder = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
