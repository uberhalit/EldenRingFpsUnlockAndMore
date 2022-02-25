using System;
using System.Runtime.InteropServices;

namespace EldenRingFPSUnlockAndMore
{
    internal class WinAPI
    {
        internal const uint PROCESS_ALL_ACCESS = 0x001F0FFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Boolean bInheritHandle,
            UInt32 dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern Boolean ReadProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern Boolean WriteProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [In, Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesWritten);
    }
}
