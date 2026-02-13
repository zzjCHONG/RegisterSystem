using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RegisterSystem;

public enum RegisterStatus
{
    未注册,
    永久,
    试用,
    超期,
}

public sealed class EnrollPayload
{
    public string MachineIdEncrypted { get; set; } = string.Empty;
    public string LastDateEncrypted { get; set; } = string.Empty;
    public string DeadlineEncrypted { get; set; } = string.Empty;
    public string EnrollCode { get; set; } = string.Empty;

    public string ToCompactString() => string.Join("|", MachineIdEncrypted, LastDateEncrypted, DeadlineEncrypted, EnrollCode);

    public static bool TryParse(string raw, out EnrollPayload payload)
    {
        payload = new EnrollPayload();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string[] parts = raw
            .Split(new[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != 4)
        {
            return false;
        }

        payload.MachineIdEncrypted = parts[0];
        payload.LastDateEncrypted = parts[1];
        payload.DeadlineEncrypted = parts[2];
        payload.EnrollCode = parts[3];
        return true;
    }
}

public static class RegisterService
{
    private static readonly byte[] Keys = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
    private const string DesKey = "A1B2C3D4";

    public static RegisterStatus RegisterStatus { get; private set; } = RegisterStatus.未注册;
    public static string MachineId { get; private set; } = string.Empty;
    public static string Deadline { get; private set; } = string.Empty;
    public static string LastDate { get; private set; } = string.Empty;

    public static string RegisterFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RegisterSystem",
        "RegisterFile",
        $"{GetMachineCode()}.json");

    public static string GetMachineCode()
    {
        string disk = GetDiskVolumeSerialNumber();
        string cpu = GetCpuId();
        string joined = string.Concat(disk, cpu);

        if (joined.Length < 24)
        {
            joined = joined.PadRight(24, '0');
        }

        return joined[..24];
    }

    public static string GetRegisterCode(string machineId)
    {
        int[] intCode = new int[127];
        char[] charCode = new char[25];
        int[] intNumber = new int[25];

        for (int i = 1; i < intCode.Length; i++)
        {
            intCode[i] = i % 9;
        }

        for (int i = 1; i < charCode.Length; i++)
        {
            charCode[i] = machineId[i - 1];
        }

        for (int i = 1; i < intNumber.Length; i++)
        {
            intNumber[i] = charCode[i] + intCode[charCode[i]];
        }

        StringBuilder result = new();
        for (int i = 1; i < intNumber.Length; i++)
        {
            int value = intNumber[i];
            if ((value >= 48 && value <= 57) || (value >= 65 && value <= 90) || (value >= 97 && value <= 122))
            {
                result.Append((char)value);
            }
            else if (value > 122)
            {
                result.Append((char)(value - 10));
            }
            else
            {
                result.Append((char)(value - 9));
            }
        }

        return EncryptDES(result.ToString());
    }

    public static string GetRegisterCode() => GetRegisterCode(GetMachineCode());

    public static EnrollPayload BuildPayload(DateTime deadlineDate, DateTime? currentDate = null)
    {
        string machineId = GetMachineCode();
        string today = (currentDate ?? DateTime.Now).ToString("yyyy/MM/dd");
        return new EnrollPayload
        {
            MachineIdEncrypted = EncryptDES(machineId),
            LastDateEncrypted = EncryptDES(today),
            DeadlineEncrypted = EncryptDES(deadlineDate.ToString("yyyy/MM/dd")),
            EnrollCode = GetRegisterCode(machineId),
        };
    }

    public static bool SaveEnrollPayload(EnrollPayload payload, out string error)
    {
        error = string.Empty;
        try
        {
            string dir = Path.GetDirectoryName(RegisterFilePath)!;
            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(RegisterFilePath, json, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryActivateFromRaw(string raw, out string error)
    {
        error = string.Empty;
        if (!EnrollPayload.TryParse(raw, out EnrollPayload payload))
        {
            error = "注册码格式不正确，必须是4段数据。";
            return false;
        }

        string currentMachineId = GetMachineCode();
        string machineId = DecryptDES(payload.MachineIdEncrypted);
        if (!string.Equals(machineId, currentMachineId, StringComparison.Ordinal))
        {
            error = "机器码不匹配，无法注册。";
            return false;
        }

        if (!string.Equals(payload.EnrollCode, GetRegisterCode(machineId), StringComparison.Ordinal))
        {
            error = "注册码校验失败。";
            return false;
        }

        string deadline = DecryptDES(payload.DeadlineEncrypted);
        if (!DateTime.TryParse(deadline, out _))
        {
            error = "使用日期格式错误。";
            return false;
        }

        payload.LastDateEncrypted = EncryptDES(DateTime.Now.ToString("yyyy/MM/dd"));
        return SaveEnrollPayload(payload, out error);
    }

    public static void ReadEnrollFile()
    {
        RegisterStatus = RegisterStatus.未注册;
        MachineId = GetMachineCode();
        Deadline = string.Empty;
        LastDate = string.Empty;

        if (!File.Exists(RegisterFilePath))
        {
            return;
        }

        try
        {
            EnrollPayload? payload = JsonSerializer.Deserialize<EnrollPayload>(File.ReadAllText(RegisterFilePath, Encoding.UTF8));
            if (payload is null)
            {
                return;
            }

            string machineId = DecryptDES(payload.MachineIdEncrypted);
            if (!string.Equals(machineId, MachineId, StringComparison.Ordinal))
            {
                return;
            }

            LastDate = DecryptDES(payload.LastDateEncrypted);
            Deadline = DecryptDES(payload.DeadlineEncrypted);

            DateTime now = DateTime.Today;
            DateTime lastDate = DateTime.Parse(LastDate);
            DateTime deadlineDate = DateTime.Parse(Deadline);
            bool isTrial = deadlineDate < DateTime.Parse("2122/12/31");

            if (now < lastDate)
            {
                RegisterStatus = RegisterStatus.未注册;
                return;
            }

            payload.LastDateEncrypted = EncryptDES(now.ToString("yyyy/MM/dd"));
            SaveEnrollPayload(payload, out _);

            if (now >= deadlineDate)
            {
                RegisterStatus = RegisterStatus.超期;
                return;
            }

            RegisterStatus = payload.EnrollCode == GetRegisterCode(MachineId)
                ? (isTrial ? RegisterStatus.试用 : RegisterStatus.永久)
                : RegisterStatus.未注册;
        }
        catch
        {
            RegisterStatus = RegisterStatus.未注册;
        }
    }

    public static string EncryptDES(string plainText)
    {
        try
        {
            byte[] rgbKey = Encoding.UTF8.GetBytes(DesKey[..8]);
            byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
            using DESCryptoServiceProvider provider = new();
            using MemoryStream ms = new();
            using CryptoStream cs = new(ms, provider.CreateEncryptor(rgbKey, Keys), CryptoStreamMode.Write);
            cs.Write(inputBytes, 0, inputBytes.Length);
            cs.FlushFinalBlock();
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return plainText;
        }
    }

    public static string DecryptDES(string cipherText)
    {
        try
        {
            byte[] rgbKey = Encoding.UTF8.GetBytes(DesKey[..8]);
            byte[] inputBytes = Convert.FromBase64String(cipherText);
            using DESCryptoServiceProvider provider = new();
            using MemoryStream ms = new();
            using CryptoStream cs = new(ms, provider.CreateDecryptor(rgbKey, Keys), CryptoStreamMode.Write);
            cs.Write(inputBytes, 0, inputBytes.Length);
            cs.FlushFinalBlock();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return cipherText;
        }
    }

    private static string GetDiskVolumeSerialNumber()
    {
        try
        {
            using ManagementObject disk = new("Win32_LogicalDisk.DeviceID=\"C:\"");
            disk.Get();
            return disk.GetPropertyValue("VolumeSerialNumber")?.ToString() ?? "DISK00000000";
        }
        catch
        {
            return "DISK00000000";
        }
    }

    private static string GetCpuId()
    {
        try
        {
            using ManagementClass cpu = new("win32_Processor");
            foreach (ManagementObject obj in cpu.GetInstances())
            {
                return obj.Properties["ProcessorId"]?.Value?.ToString() ?? "CPU00000000000000";
            }
        }
        catch
        {
        }

        return "CPU00000000000000";
    }
}
