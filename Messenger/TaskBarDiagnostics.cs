using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Messenger
{
    /// <summary>
    /// Diagnostics class for checking the integrity level, job object status, and parent process of the current process.
    /// This is a temporary solution until a more robust method is implemented, as the current approach may not be reliable in all scenarios.
    /// </summary>
    public static class TaskbarDiagnostics
    {
        // -----------------------------
        // P/Invoke
        // -----------------------------

        private const int TokenIntegrityLevel = 25;
        private const uint TOKEN_QUERY = 0x0008;

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_MANDATORY_LABEL
        {
            public IntPtr Label; // SID_AND_ATTRIBUTES*
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            int TokenInformationClass,
            IntPtr TokenInformation,
            int TokenInformationLength,
            out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsProcessInJob(
            IntPtr hProcess,
            IntPtr hJob,
            out bool result);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        // -----------------------------
        // PUBLIC ENTRYPOINT
        // -----------------------------

        public static void Dump()
        {
            Debug.WriteLine("=== Taskbar Diagnostics ===");
            Debug.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName} (PID {Process.GetCurrentProcess().Id})");
            Debug.WriteLine($"Is64BitProcess: {Environment.Is64BitProcess}");
            Debug.WriteLine($"Integrity Level: {GetIntegrityLevel()}");
            Debug.WriteLine($"IsInJob: {IsInJob()}");
            Debug.WriteLine($"IsLikelyAppContainer: {IsLikelyAppContainer()}");

            var parent = GetParentProcess();
            Debug.WriteLine($"Parent process: {parent?.ProcessName ?? "<unknown>"} (PID {parent?.Id.ToString() ?? "?"})");

            Debug.WriteLine("=== End Diagnostics ===");
        }

        // -----------------------------
        // INTEGRITY LEVEL
        // -----------------------------

        public static string GetIntegrityLevel()
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_QUERY, out IntPtr tokenHandle))
                return "Unknown (OpenProcessToken failed)";

            int length = 0;
            GetTokenInformation(tokenHandle, TokenIntegrityLevel, IntPtr.Zero, 0, out length);

            IntPtr buffer = Marshal.AllocHGlobal(length);

            try
            {
                if (!GetTokenInformation(tokenHandle, TokenIntegrityLevel, buffer, length, out length))
                    return "Unknown (GetTokenInformation failed)";

                TOKEN_MANDATORY_LABEL tml =
                    Marshal.PtrToStructure<TOKEN_MANDATORY_LABEL>(buffer);

                var sid = new SecurityIdentifier(tml.Label);

                int rid = sid.GetRid();

                return rid switch
                {
                    0x1000 => "Low",
                    0x2000 => "Medium",
                    0x3000 => "High",
                    0x4000 => "System",
                    0x5000 => "Protected",
                    _ => $"Unknown (RID=0x{rid:X})"
                };
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int GetRid(this SecurityIdentifier sid)
        {
            var parts = sid.Value.Split('-');
            return int.Parse(parts[^1]);
        }

        // -----------------------------
        // JOB OBJECT
        // -----------------------------

        public static bool IsInJob()
        {
            bool result;
            if (!IsProcessInJob(GetCurrentProcess(), IntPtr.Zero, out result))
                return false;
            return result;
        }

        // -----------------------------
        // APP CONTAINER HEURISTICS
        // -----------------------------

        public static bool IsLikelyAppContainer()
        {
            string il = GetIntegrityLevel();
            return il == "Low"; // AppContainer → Low IL
        }

        // -----------------------------
        // PARENT PROCESS (utan System.Management)
        // -----------------------------

        public static Process? GetParentProcess()
        {
            try
            {
                using var current = Process.GetCurrentProcess();
                int ppid = ParentPidNative(current.Id);
                if (ppid <= 0)
                    return null;

                return Process.GetProcessById(ppid);
            }
            catch
            {
                return null;
            }
        }

        // Native parent PID via NtQueryInformationProcess
        private static int ParentPidNative(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                var pbi = new PROCESS_BASIC_INFORMATION();
                int returnLength = 0;

                int status = NtQueryInformationProcess(
                    proc.Handle,
                    0, // ProcessBasicInformation
                    ref pbi,
                    Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(),
                    ref returnLength);

                if (status != 0)
                    return -1;

                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
            catch
            {
                return -1;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            int processInformationLength,
            ref int returnLength);
    }
}