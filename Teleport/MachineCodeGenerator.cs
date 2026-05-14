using System;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Teleport
{

    internal class MachineCodeGenerator
    {
        private static string BiosId()
        {
            var s = "Moewcorp Desu~";
            return GetHash(s);
        }
        private static string GetHash(string s) => GetHexString(MD5.HashData(new ASCIIEncoding().GetBytes(s)));

        private static string GetHexString(byte[] bt)
        {
            var hexString = string.Empty;
            for (var index = 0; index < bt.Length; ++index)
            {
                var num1 = (int)bt[index];
                var num2 = num1 & 15;
                var num3 = (num1 >> 4) & 15;
                var str = num3 <= 9 ? hexString + num3.ToString() : hexString + ((char)(num3 - 10 + 65)).ToString();
                hexString = num2 <= 9 ? str + num2.ToString() : str + ((char)(num2 - 10 + 65)).ToString();
                if (index + 1 != bt.Length && (index + 1) % 2 == 0)
                    hexString ??= "";
            }
            return hexString;
        }
        internal static string GenerateMachineCode()
        {
            var cpuId = GetCpuId();
            var diskId = GetDiskId();

            if (string.IsNullOrEmpty(cpuId) || string.IsNullOrEmpty(diskId))
            {
                throw new Exception("Unable to fetch hardware information");
            }

            // 你可以选择你自己的机器码生成方式，这里只是一个例子
            var machineCode = cpuId + "-" + diskId;

            return machineCode;
        }
        internal static string GenerateMachineCodeV2()
        {
            var cpuId = GetCpuId();
            var diskId = GetDiskId();
            var biosId = BiosId();

            if (string.IsNullOrEmpty(cpuId) || string.IsNullOrEmpty(diskId) || string.IsNullOrEmpty(biosId))
            {
                throw new Exception("Unable to fetch hardware information");
            }

            // 你可以选择你自己的机器码生成方式，这里只是一个例子
            var machineCode = cpuId + "-" + diskId + "-" + biosId;

            return GetHash(machineCode);
        }
        private static string GetCpuId()
        {
            return "Cpuid";
        }

        private static string GetDiskId()
        {
            return "DiskId";
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
            // Add salt to the machine code
            var saltedMachineCode = machineCode + salt;

            // Compute and get hash
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedMachineCode));

            // Convert byte array to a string
            var builder = new StringBuilder();
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

}
