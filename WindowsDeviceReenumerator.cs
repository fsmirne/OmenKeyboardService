#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace OmenKeyboardService;

/// <summary>
/// Programmatically disables and re-enables the HP Omen keyboard's USB composite device via
/// SetupAPI — equivalent to a physical unplug/replug. Used to recover from the "signal-only
/// KVM" failure mode where Windows thinks the device is attached but the firmware has reset.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsDeviceReenumerator
{
    private const uint DIGCF_PRESENT = 0x02;
    private const uint DIGCF_ALLCLASSES = 0x04;
    private const uint DIF_PROPERTYCHANGE = 0x12;
    private const uint DICS_ENABLE = 0x01;
    private const uint DICS_DISABLE = 0x02;
    private const uint DICS_FLAG_GLOBAL = 0x01;
    private const uint SPDRP_HARDWAREID = 0x01;

    /// <summary>
    /// Disables then re-enables every top-level USB device matching the given VID/PID, forcing
    /// Windows to tear down and re-enumerate it (and all of its interface children). Returns
    /// true if at least one matching device was toggled successfully.
    /// </summary>
    public static bool ToggleDevice(int vendorId, int productId)
    {
        string hardwareIdMatch = $"USB\\VID_{vendorId:X4}&PID_{productId:X4}";

        IntPtr deviceInfoSet = SetupDiGetClassDevs(IntPtr.Zero, "USB", IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
        if (deviceInfoSet == new IntPtr(-1))
            return false;

        try
        {
            var devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            bool anyToggled = false;

            for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfoData); i++)
            {
                string? hwId = GetHardwareId(deviceInfoSet, ref devInfoData);
                if (hwId is null)
                    continue;

                // Composite device hardware IDs look like "USB\VID_03F0&PID_1F41&REV_XXXX" or
                // "USB\VID_03F0&PID_1F41". Interface children include "&MI_XX" — skip those so we
                // toggle the parent composite device once, which re-enumerates all interfaces.
                if (!hwId.StartsWith(hardwareIdMatch, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (hwId.Contains("&MI_", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SetDeviceState(deviceInfoSet, ref devInfoData, DICS_DISABLE))
                    continue;

                Thread.Sleep(500);

                if (!SetDeviceState(deviceInfoSet, ref devInfoData, DICS_ENABLE))
                    continue;

                anyToggled = true;
            }

            return anyToggled;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }
    }

    private static string? GetHardwareId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData)
    {
        byte[] buffer = new byte[1024];
        if (!SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref devInfoData, SPDRP_HARDWAREID, out _, buffer, (uint)buffer.Length, out _))
            return null;

        // SPDRP_HARDWAREID returns a multi-sz (double-null-terminated list of UTF-16 strings).
        // The first entry is the most specific hardware ID — that's all we need to match on.
        string raw = Encoding.Unicode.GetString(buffer);
        int nullIdx = raw.IndexOf('\0');
        return nullIdx >= 0 ? raw.Substring(0, nullIdx) : raw;
    }

    private static bool SetDeviceState(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData, uint stateChange)
    {
        var propChangeParams = new SP_PROPCHANGE_PARAMS
        {
            ClassInstallHeader = new SP_CLASSINSTALL_HEADER
            {
                cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(),
                InstallFunction = DIF_PROPERTYCHANGE,
            },
            StateChange = stateChange,
            Scope = DICS_FLAG_GLOBAL,
            HwProfile = 0,
        };

        int size = Marshal.SizeOf<SP_PROPCHANGE_PARAMS>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(propChangeParams, ptr, false);
            if (!SetupDiSetClassInstallParams(deviceInfoSet, ref devInfoData, ptr, (uint)size))
                return false;
            return SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, deviceInfoSet, ref devInfoData);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string? Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, IntPtr ClassInstallParams, uint ClassInstallParamsSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(uint InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);
}
#endif
