using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Allows child processes (such as Python backend) to be automatically killed by the
/// Windows OS kernel when the parent process (ScripOx Desktop) terminates.
/// </summary>
public static class ChildProcessTracker
{
    private static readonly IntPtr JobHandle = IntPtr.Zero;

    static ChildProcessTracker()
    {
        try
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            JobHandle = CreateJobObject(IntPtr.Zero, null);
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = 0x2000 // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            };
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = info
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                SetInformationJobObject(JobHandle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length);
            }
            finally
            {
                Marshal.FreeHGlobal(extendedInfoPtr);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize Windows Job Object for child process tracking");
        }
    }

    public static void AddProcess(Process process)
    {
        if (JobHandle != IntPtr.Zero && process is { HasExited: false })
        {
            try
            {
                AssignProcessToJobObject(JobHandle, process.Handle);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to attach child process {ProcessId} to Windows Job Object", process.Id);
            }
        }
    }

    #region Win32 P/Invoke
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }
    #endregion
}
