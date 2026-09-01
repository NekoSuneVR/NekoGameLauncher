using NekoGameLauncher.Models;
using System.Runtime.InteropServices;

namespace NekoGameLauncher.Services;

public sealed class SystemPerformanceService
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuSample;

    public SystemPerformanceSnapshot GetSnapshot()
    {
        var cpu = ReadCpu();
        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref memory))
            return new SystemPerformanceSnapshot { CpuPercent = cpu };

        var total = memory.TotalPhysical / 1024d / 1024d / 1024d;
        var available = memory.AvailablePhysical / 1024d / 1024d / 1024d;
        var used = Math.Max(0, total - available);
        return new SystemPerformanceSnapshot
        {
            CpuPercent = cpu,
            MemoryTotalGb = total,
            MemoryUsedGb = used,
            MemoryPercent = total <= 0 ? 0 : used / total * 100
        };
    }

    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var idleValue = ToUInt64(idle);
        var kernelValue = ToUInt64(kernel);
        var userValue = ToUInt64(user);

        if (!_hasCpuSample)
        {
            _hasCpuSample = true;
            _lastIdle = idleValue;
            _lastKernel = kernelValue;
            _lastUser = userValue;
            return 0;
        }

        var idleDelta = idleValue - _lastIdle;
        var kernelDelta = kernelValue - _lastKernel;
        var userDelta = userValue - _lastUser;
        _lastIdle = idleValue;
        _lastKernel = kernelValue;
        _lastUser = userValue;

        var total = kernelDelta + userDelta;
        if (total == 0) return 0;
        return Math.Clamp((1d - idleDelta / (double)total) * 100d, 0d, 100d);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
