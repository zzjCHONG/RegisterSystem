using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

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
    private const string DateFormat = "yyyy/MM/dd";

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
        => BuildPayload(GetMachineCode(), deadlineDate, currentDate);

    public static EnrollPayload BuildPayload(string machineId, DateTime deadlineDate, DateTime? currentDate = null)
    {
        string machine = string.IsNullOrWhiteSpace(machineId) ? GetMachineCode() : machineId.Trim();
        string today = (currentDate ?? DateTime.Now).ToString(DateFormat);
        return new EnrollPayload
        {
            MachineIdEncrypted = EncryptDES(machine),
            LastDateEncrypted = EncryptDES(today),
            DeadlineEncrypted = EncryptDES(deadlineDate.ToString(DateFormat)),
            EnrollCode = GetRegisterCode(machine),
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
        if (!TryDecryptDES(payload.MachineIdEncrypted, out string machineId))
        {
            error = "机器码数据无效。";
            return false;
        }

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

        if (!TryDecryptDES(payload.DeadlineEncrypted, out string deadline))
        {
            error = "使用日期数据无效。";
            return false;
        }

        if (!DateTime.TryParseExact(deadline, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deadlineDate))
        {
            error = "使用日期格式错误。";
            return false;
        }

        if (deadlineDate.Date < DateTime.Today)
        {
            error = "授权已过期，无法激活。";
            return false;
        }

        payload.LastDateEncrypted = EncryptDES(DateTime.Now.ToString(DateFormat));
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

            if (!TryDecryptDES(payload.MachineIdEncrypted, out string machineId))
            {
                return;
            }

            if (!string.Equals(machineId, MachineId, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryDecryptDES(payload.LastDateEncrypted, out string lastDateRaw)
                || !TryDecryptDES(payload.DeadlineEncrypted, out string deadlineRaw))
            {
                return;
            }

            LastDate = lastDateRaw;
            Deadline = deadlineRaw;

            if (!DateTime.TryParseExact(LastDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime lastDate)
                || !DateTime.TryParseExact(Deadline, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime deadlineDate))
            {
                return;
            }

            DateTime now = DateTime.Today;
            bool isTrial = deadlineDate < DateTime.ParseExact("2122/12/31", DateFormat, CultureInfo.InvariantCulture);

            if (now < lastDate)
            {
                RegisterStatus = RegisterStatus.未注册;
                return;
            }

            payload.LastDateEncrypted = EncryptDES(now.ToString(DateFormat));
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
        return TryDecryptDES(cipherText, out string plainText) ? plainText : cipherText;
    }

    public static bool TryDecryptDES(string cipherText, out string plainText)
    {
        plainText = string.Empty;
        try
        {
            if (!IsBase64(cipherText))
            {
                return false;
            }

            byte[] rgbKey = Encoding.UTF8.GetBytes(DesKey[..8]);
            byte[] inputBytes = Convert.FromBase64String(cipherText);
            using DESCryptoServiceProvider provider = new();
            using MemoryStream ms = new();
            using CryptoStream cs = new(ms, provider.CreateDecryptor(rgbKey, Keys), CryptoStreamMode.Write);
            cs.Write(inputBytes, 0, inputBytes.Length);
            cs.FlushFinalBlock();
            plainText = Encoding.UTF8.GetString(ms.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsBase64(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length % 4 != 0)
        {
            return false;
        }

        return Convert.TryFromBase64String(input, new Span<byte>(new byte[input.Length]), out _);
    }

    private static bool IsBase64(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length % 4 != 0)
        {
            return false;
        }

        return Convert.TryFromBase64String(input, new Span<byte>(new byte[input.Length]), out _);
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
