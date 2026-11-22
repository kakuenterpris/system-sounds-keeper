using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SystemSoundsKeeper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        public Guid formatId;
        public uint propertyId;
        public PropertyKey(Guid formatId, uint propertyId) { this.formatId = formatId; this.propertyId = propertyId; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort type;
        [FieldOffset(8)] public IntPtr pointer;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object value);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, uint flags, out IntPtr control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, uint flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string session, IntPtr notification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, ref Guid context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float volume, ref Guid context);
        [PreserveSig] int GetMasterVolume(out float volume);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
    }

    public sealed class SessionInfo
    {
        public string instanceId { get; set; }
        public bool isSystemSounds { get; set; }
        public uint processId { get; set; }
        public int state { get; set; }
        public double volumePercent { get; set; }
        public bool muted { get; set; }
    }

    public sealed class DeviceInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public uint state { get; set; }
        public string stateName { get; set; }
        public List<string> defaultRoles { get; set; }
        public double? masterVolumePercent { get; set; }
        public List<SessionInfo> sessions { get; set; }
        public string error { get; set; }
    }

    internal sealed class AudioApi : IDisposable
    {
        private IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        private IMMNotificationClient callback;
        private static readonly Guid Context = new Guid("55FE8BB5-4B70-48D8-A66A-020A3D990218");

        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
        private static void Check(int result) { Marshal.ThrowExceptionForHR(result); }
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

        public void Subscribe(IMMNotificationClient client)
        {
            Check(enumerator.RegisterEndpointNotificationCallback(client));
            callback = client;
        }

        private static string FriendlyName(IMMDevice device)
        {
            IPropertyStore store = null;
            PropVariant value = new PropVariant();
            try
            {
                Check(device.OpenPropertyStore(0, out store));
                var key = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
                Check(store.GetValue(ref key, out value));
                return value.type == 31 ? Marshal.PtrToStringUni(value.pointer) : "Unknown output device";
            }
            finally { PropVariantClear(ref value); Release(store); }
        }

        private Dictionary<string, List<string>> Defaults()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string[] roles = { "Console", "Multimedia", "Communications" };
            for (int role = 0; role < roles.Length; role++)
            {
                IMMDevice device = null;
                try
                {
                    int hr = enumerator.GetDefaultAudioEndpoint(0, role, out device);
                    if (hr == unchecked((int)0x80070490)) continue; // No default device.
                    Check(hr);
                    string id;
                    Check(device.GetId(out id));
                    if (!result.ContainsKey(id)) result[id] = new List<string>();
                    result[id].Add(roles[role]);
                }
                finally { Release(device); }
            }
            return result;
        }

        public List<DeviceInfo> Snapshot(bool includeInactive, bool diagnostics)
        {
            var result = new List<DeviceInfo>();
            var defaults = Defaults();
            IMMDeviceCollection collection = null;
            try
            {
                Check(enumerator.EnumAudioEndpoints(0, includeInactive ? 15u : 1u, out collection));
                uint count;
                Check(collection.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        Check(collection.Item(i, out device));
                        string id;
                        uint state;
                        Check(device.GetId(out id));
                        Check(device.GetState(out state));
                        var info = new DeviceInfo {
                            id = id, state = state,
                            stateName = state == 1 ? "Active" : state == 2 ? "Disabled" : state == 4 ? "NotPresent" : state == 8 ? "Unplugged" : state.ToString(),
                            defaultRoles = defaults.ContainsKey(id) ? defaults[id] : new List<string>(),
                            sessions = new List<SessionInfo>()
                        };
                        try { info.name = FriendlyName(device); } catch { info.name = id; }
                        if ((state & 1) != 0)
                        {
                            try
                            {
                                if (diagnostics) info.masterVolumePercent = ReadMaster(device);
                                VisitSessions(device, delegate(object session, IAudioSessionControl2 control) {
                                    bool system = control.IsSystemSoundsSession() == 0;
                                    if (!system && !diagnostics) return;
                                    var item = new SessionInfo { isSystemSounds = system };
                                    string instanceId;
                                    uint processId;
                                    int sessionState;
                                    Check(control.GetSessionInstanceIdentifier(out instanceId));
                                    Check(control.GetState(out sessionState));
                                    if (sessionState == 2) return;
                                    Check(control.GetProcessId(out processId));
                                    float volume;
                                    bool muted;
                                    var simpleVolume = (ISimpleAudioVolume)session;
                                    Check(simpleVolume.GetMasterVolume(out volume));
                                    Check(simpleVolume.GetMute(out muted));
                                    item.instanceId = instanceId;
                                    item.state = sessionState;
                                    item.processId = processId;
                                    item.volumePercent = Math.Round(volume * 100, 4);
                                    item.muted = muted;
                                    info.sessions.Add(item);
                                });
                            }
                            catch (Exception ex) { info.error = ex.Message; }
                        }
                        result.Add(info);
                    }
                    finally { Release(device); }
                }
            }
            finally { Release(collection); }
            return result;
        }

        private static double ReadMaster(IMMDevice device)
        {
            object instance = null;
            try
            {
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out instance));
                float value;
                Check(((IAudioEndpointVolume)instance).GetMasterVolumeLevelScalar(out value));
                return Math.Round(value * 100, 4);
            }
            finally { Release(instance); }
        }

        private static void VisitSessions(IMMDevice device, Action<object, IAudioSessionControl2> action)
        {
            object managerObject = null;
            IAudioSessionEnumerator sessions = null;
            try
            {
                Guid iid = typeof(IAudioSessionManager2).GUID;
                Check(device.Activate(ref iid, 23, IntPtr.Zero, out managerObject));
                Check(((IAudioSessionManager2)managerObject).GetSessionEnumerator(out sessions));
                int count;
                Check(sessions.GetCount(out count));
                for (int i = 0; i < count; i++)
                {
                    object session = null;
                    try
                    {
                        Check(sessions.GetSession(i, out session));
                        action(session, (IAudioSessionControl2)session);
                    }
                    finally { Release(session); }
                }
            }
            finally { Release(sessions); Release(managerObject); }
        }

        public bool Apply(string deviceId, string instanceId, double percent, Action<string> log)
        {
            IMMDevice device = null;
            bool found = false;
            try
            {
                Check(enumerator.GetDevice(deviceId, out device));
                VisitSessions(device, delegate(object session, IAudioSessionControl2 control) {
                    if (control.IsSystemSoundsSession() != 0) return;
                    string id;
                    int state;
                    Check(control.GetSessionInstanceIdentifier(out id));
                    Check(control.GetState(out state));
                    if (state == 2 || !String.Equals(id, instanceId, StringComparison.Ordinal)) return;
                    var volume = (ISimpleAudioVolume)session;
                    float before;
                    Check(volume.GetMasterVolume(out before));
                    float target = (float)(percent / 100.0);
                    if (Math.Abs(before - target) > 0.0001)
                    {
                        Guid context = Context;
                        Check(volume.SetMasterVolume(target, ref context));
                    }
                    float after;
                    Check(volume.GetMasterVolume(out after));
                    if (Math.Abs(after - target) > 0.0001) throw new InvalidOperationException("Volume readback did not match target.");
                    log("System Sounds " + deviceId + ": " + Math.Round(before * 100, 2) + "% -> " + percent + "%");
                    found = true;
                });
                return found;
            }
            finally { Release(device); }
        }

        public void Dispose()
        {
            if (enumerator == null) return;
            if (callback != null) { try { enumerator.UnregisterEndpointNotificationCallback(callback); } catch { } }
            Release(enumerator);
            enumerator = null;
            callback = null;
        }
    }
}
