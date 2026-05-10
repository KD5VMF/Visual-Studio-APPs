using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AdGuardLiveWatch;

internal static class PasswordVault
{
    private const string Prefix = "dpapi:";
    private const string Purpose = "AGWatch_REV9_AdGuardHome_UserPassword_v1";

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] clearBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] entropyBytes = Encoding.UTF8.GetBytes(Purpose);
            byte[] protectedBytes = CryptProtect(clearBytes, entropyBytes);
            return Prefix + Convert.ToBase64String(protectedBytes);
        }
        catch
        {
            // Never fall back to writing the password in plain text.
            // The user can re-enter it if Windows DPAPI is unavailable.
            return string.Empty;
        }
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
            return string.Empty;

        if (!protectedText.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        try
        {
            string base64 = protectedText[Prefix.Length..];
            byte[] protectedBytes = Convert.FromBase64String(base64);
            byte[] entropyBytes = Encoding.UTF8.GetBytes(Purpose);
            byte[] clearBytes = CryptUnprotect(protectedBytes, entropyBytes);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static byte[] CryptProtect(byte[] clearBytes, byte[] entropyBytes)
    {
        DATA_BLOB dataIn = CreateBlob(clearBytes);
        DATA_BLOB entropy = CreateBlob(entropyBytes);
        DATA_BLOB dataOut = default;

        try
        {
            if (!CryptProtectData(ref dataIn, "AGWatch REV9 saved AdGuard password", ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out dataOut))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return ReadAndFreeLocalBlob(dataOut);
        }
        finally
        {
            FreeInputBlob(dataIn);
            FreeInputBlob(entropy);
        }
    }

    private static byte[] CryptUnprotect(byte[] protectedBytes, byte[] entropyBytes)
    {
        DATA_BLOB dataIn = CreateBlob(protectedBytes);
        DATA_BLOB entropy = CreateBlob(entropyBytes);
        DATA_BLOB dataOut = default;
        IntPtr description = IntPtr.Zero;

        try
        {
            if (!CryptUnprotectData(ref dataIn, out description, ref entropy, IntPtr.Zero, IntPtr.Zero, 0, out dataOut))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            return ReadAndFreeLocalBlob(dataOut);
        }
        finally
        {
            if (description != IntPtr.Zero)
                LocalFree(description);
            FreeInputBlob(dataIn);
            FreeInputBlob(entropy);
        }
    }

    private static DATA_BLOB CreateBlob(byte[] bytes)
    {
        if (bytes.Length == 0)
            return new DATA_BLOB();

        var blob = new DATA_BLOB
        {
            cbData = bytes.Length,
            pbData = Marshal.AllocHGlobal(bytes.Length)
        };
        Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
        return blob;
    }

    private static void FreeInputBlob(DATA_BLOB blob)
    {
        if (blob.pbData != IntPtr.Zero)
            Marshal.FreeHGlobal(blob.pbData);
    }

    private static byte[] ReadAndFreeLocalBlob(DATA_BLOB blob)
    {
        try
        {
            if (blob.pbData == IntPtr.Zero || blob.cbData <= 0)
                return Array.Empty<byte>();

            byte[] bytes = new byte[blob.cbData];
            Marshal.Copy(blob.pbData, bytes, 0, blob.cbData);
            return bytes;
        }
        finally
        {
            if (blob.pbData != IntPtr.Zero)
                LocalFree(blob.pbData);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string? szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        out IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
