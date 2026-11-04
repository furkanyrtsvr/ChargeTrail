using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace PilTakip
{
    [DataContract] public class TestRun
    {
        [DataMember] public string Started = DateTime.UtcNow.ToString("o");
        [DataMember] public string Ended;
        [DataMember] public string Name;
        [DataMember] public string DeviceId;
        [DataMember] public string Connection;
        [DataMember] public double VisibleSeconds;
        [DataMember] public double ActiveSeconds;
        [DataMember] public int IdleSeconds = 60;
        [DataMember] public bool Manual;
        [DataMember] public double PcSeconds;
        [DataMember] public string Notes;
        [DataMember] public string PollingRate;
    }
    [DataContract] public class TrackedDevice
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public string Kind;
        [DataMember] public string Connection;
        [DataMember] public bool Enabled;
        [DataMember] public bool Manual;
        [DataMember] public TestRun Run;
        [DataMember] public bool Pinned;
        [IgnoreDataMember] public bool Present;
        [IgnoreDataMember] public double ActiveUntil;
        [IgnoreDataMember] public DateTime? LastInput;
    }
    [DataContract] public class State
    {
        [DataMember] public int Version = 1;
        [DataMember] public int IdleSeconds = 60;
        [DataMember] public List<TrackedDevice> Devices = new List<TrackedDevice>();
        [DataMember] public List<TestRun> History = new List<TestRun>();
        [DataMember] public bool ManualInitialized;
        [DataMember] public bool DashboardInitialized;
        [DataMember] public string Language;
        public TrackedDevice AddManual(string name)
        {
            var d = new TrackedDevice { Id = "manual:" + Guid.NewGuid().ToString("N"), Name = name.Trim(), Kind = "Manuel (PC)", Connection = "Manuel", Manual = true, Pinned = true };
            d.Run = new TestRun { Name = d.Name, DeviceId = d.Id, Connection = d.Connection, Manual = true, IdleSeconds = IdleSeconds };
            Devices.Insert(0, d);
            return d;
        }
    }
    public static class Storage
    {
        public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PilTakip");
        public static string DataPath { get { return Path.Combine(Folder, "tests.json"); } }
        public static string Warning;
        public static State Read(string path)
        {
            using (var f = File.OpenRead(path))
            {
                var s = (State)new DataContractJsonSerializer(typeof(State)).ReadObject(f);
                if (s == null || s.Version != 1 || s.Devices == null || s.History == null || s.IdleSeconds < 1 || s.Devices.Any(d => d == null || d.Id == null || d.Run == null))
                    throw new InvalidDataException("Kayıt biçimi geçersiz.");
                return s;
            }
        }
        public static State Load()
        {
            if (!File.Exists(DataPath) && !File.Exists(DataPath + ".bak")) return new State();
            try { return Read(DataPath); }
            catch
            {
                var state = Read(DataPath + ".bak");
                if (File.Exists(DataPath)) File.Copy(DataPath, DataPath + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                File.Copy(DataPath + ".bak", DataPath, true);
                Warning = "Ana kayıt okunamadı; son yedek kurtarıldı. Hasarlı kayıt klasörde korundu.";
                return state;
            }
        }
        public static void Save(State state)
        {
            Directory.CreateDirectory(Folder);
            string temp = DataPath + ".tmp";
            using (var f = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(State)).WriteObject(f, state);
                f.Flush(true);
            }
            if (File.Exists(DataPath)) File.Replace(temp, DataPath, DataPath + ".bak");
            else File.Move(temp, DataPath);
        }
    }
    public class DeviceInfo
    {
        public IntPtr Handle;
        public string Id, Name, Kind, Transport;
    }
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)] public struct DeviceList { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] public struct RawRegistration { public ushort Page, Usage; public uint Flags; public IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type, Size; public IntPtr Device, Param; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Id; public uint Pid; }
        [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] DeviceList[] devices, ref uint count, uint size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);
        [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW")] static extern uint DeviceInfoBytes(IntPtr device, uint command, IntPtr data, ref uint size);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices(RawRegistration[] devices, uint count, uint size);
        [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr input, uint command, out Header header, ref uint size, uint headerSize);
        [DllImport("kernel32.dll")] static extern bool QueryUnbiasedInterruptTime(out ulong time);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr sa, uint creation, uint flags, IntPtr template);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle device, StringBuilder text, int bytes);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint node, string id, uint flags);
        [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint node, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint node, StringBuilder id, int length, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, byte[] buffer, ref uint length, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_DevNode_PropertyW(uint node, ref PropertyKey key, out uint type, byte[] buffer, ref uint length, uint flags);
        public static double Now()
        {
            ulong time;
            if (!QueryUnbiasedInterruptTime(out time)) throw new System.ComponentModel.Win32Exception();
            return time / 10000000.0;
        }
        public static IntPtr InputDevice(IntPtr input)
        {
            Header header;
            uint size = (uint)Marshal.SizeOf(typeof(Header));
            return GetRawInputData(input, 0x10000005, out header, ref size, size) == uint.MaxValue ? IntPtr.Zero : header.Device;
        }
        static string Property(uint node, uint property)
        {
            byte[] bytes = new byte[2048]; uint len = (uint)bytes.Length, type;
            return CM_Get_DevNode_Registry_PropertyW(node, property, out type, bytes, ref len, 0) == 0 ? Encoding.Unicode.GetString(bytes, 0, (int)len).TrimEnd('\0') : "";
        }
        static string BusName(uint node)
        {
            var key = new PropertyKey { Id = new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), Pid = 4 };
            var bytes = new byte[2048]; uint length = (uint)bytes.Length, type;
            return CM_Get_DevNode_PropertyW(node, ref key, out type, bytes, ref length, 0) == 0 ? Encoding.Unicode.GetString(bytes, 0, (int)length).TrimEnd('\0') : "";
        }
        public static List<DeviceInfo> Enumerate()
        {
            uint size = (uint)Marshal.SizeOf(typeof(DeviceList));
            for (int attempt = 0; attempt < 4; attempt++)
            {
                uint count = 0;
                if (GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) continue;
                var devices = new DeviceList[count];
                uint result = GetRawInputDeviceList(devices, ref count, size);
                if (result == uint.MaxValue) continue;
                var found = new List<DeviceInfo>();
                foreach (var raw in devices.Take((int)result))
                {
                    string kind = raw.Type == 0 ? "Mouse" : raw.Type == 1 ? "Klavye" : "";
                    if (raw.Type == 2)
                    {
                        IntPtr info = Marshal.AllocHGlobal(32);
                        try
                        {
                            Marshal.WriteInt32(info, 32); uint n = 32;
                            if (DeviceInfoBytes(raw.Handle, 0x2000000b, info, ref n) != uint.MaxValue)
                            {
                                int page = (ushort)Marshal.ReadInt16(info, 20), usage = (ushort)Marshal.ReadInt16(info, 22);
                                if (page == 1 && (usage == 4 || usage == 5 || usage == 8)) kind = "Oyun kumandası";
                            }
                        }
                        finally { Marshal.FreeHGlobal(info); }
                    }
                    if (kind == "") continue;
                    uint chars = 0;
                    GetRawInputDeviceInfo(raw.Handle, 0x20000007, null, ref chars);
                    if (chars == 0) continue;
                    var path = new StringBuilder((int)chars + 1);
                    if (GetRawInputDeviceInfo(raw.Handle, 0x20000007, path, ref chars) == uint.MaxValue) continue;
                    string id = path.ToString();
                    string product = "", friendly = "", busName = "", transport = "USB / belirsiz";
                    using (var file = CreateFile(id, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                    {
                        var text = new StringBuilder(256);
                        if (!file.IsInvalid && HidD_GetProductString(file, text, 512)) product = text.ToString().Trim();
                    }
                    string instance = id.StartsWith(@"\\?\") ? id.Substring(4) : id;
                    int suffix = instance.LastIndexOf("#{", StringComparison.Ordinal);
                    if (suffix >= 0) instance = instance.Substring(0, suffix);
                    instance = instance.Replace('#', '\\');
                    uint node;
                    if (CM_Locate_DevNodeW(out node, instance, 0) == 0)
                    {
                        for (int depth = 0; depth < 8; depth++)
                        {
                            var nodeId = new StringBuilder(1024);
                            CM_Get_Device_IDW(node, nodeId, nodeId.Capacity, 0);
                            string ancestorId = nodeId.ToString();
                            bool bluetooth = ancestorId.StartsWith("BTH", StringComparison.OrdinalIgnoreCase);
                            if (bluetooth) transport = "Bluetooth";
                            // Do not borrow the name of a USB hub, host controller or Bluetooth radio.
                            bool endpoint = depth == 0 || ancestorId.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase) || ancestorId.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase) || ancestorId.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase);
                            string value = Property(node, 0x0d);
                            if (friendly == "" && value != "" && (depth == 0 || bluetooth && endpoint)) friendly = value;
                            if (busName == "" && endpoint) busName = BusName(node);
                            if (product == "" && depth == 0) product = Property(node, 1);
                            uint parent;
                            if (CM_Get_Parent(out parent, node, 0) != 0) break;
                            node = parent;
                        }
                    }
                    found.Add(new DeviceInfo { Handle = raw.Handle, Id = id, Name = friendly != "" ? friendly : busName != "" ? busName : product != "" ? product : kind, Kind = kind, Transport = transport });
                }
                return found;
            }
            throw new IOException("Windows cihaz listesi okunamadı. Yenile ile tekrar deneyin.");
        }
    }
    public static class Counter
    {
        public static void Advance(TrackedDevice device, double from, double to, bool locked)
        {
            if (!device.Enabled || device.Run.Ended != null || to <= from) return;
            if (device.Manual) { device.Run.PcSeconds += to - from; return; }
            if (!device.Present) return;
            device.Run.VisibleSeconds += to - from;
            if (!locked) device.Run.ActiveSeconds += Math.Max(0, Math.Min(to, device.ActiveUntil) - from);
        }
    }
    static class Program
    {
        [STAThread] static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Contains("--self-test")) { SelfTest.Run(args); return; }
            if (args.Contains("--integration-test")) { SelfTest.Integration(args); return; }
            string sid = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            bool created;
            using (var mutex = new Mutex(true, @"Local\PilTakip-" + sid, out created))
            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PilTakip-Show-" + sid))
            {
                if (!created) { signal.Set(); return; }
                try
                {
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                    using (var form = new MainForm(Storage.Load(), args.Contains("--background"), signal)) Application.Run(form);
                }
                catch (Exception ex) { MessageBox.Show("ChargeTrail could not start or encountered an error.\n\n" + ex.Message + "\n\nData folder: " + Storage.Folder, "ChargeTrail", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
    static class SelfTest
    {
        static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); }
        public static void Integration(string[] args)
        {
            string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(Path.GetTempPath(), "PilTakip-integration.txt");
            Storage.Folder = Path.Combine(Path.GetTempPath(), "PilTakip-check-" + Guid.NewGuid().ToString("N"));
            var data = new State { ManualInitialized = true };
            var manual = data.AddManual("SC380 Pro"); manual.Enabled = true;
            var endpoint = Native.Enumerate().First();
            var device = new TrackedDevice { Id = endpoint.Id, Name = endpoint.Name, Kind = endpoint.Kind, Connection = endpoint.Transport, Enabled = true, Pinned = true };
            device.Run = new TestRun { DeviceId = device.Id, Name = device.Name, IdleSeconds = 60 };
            data.Devices.Add(device);
            using (var signal = new EventWaitHandle(false, EventResetMode.AutoReset))
            using (var form = new MainForm(data, true, signal))
            using (var tick = new System.Windows.Forms.Timer { Interval = 1600 })
            {
                int step = 0; double beforeHide = 0;
                form.Load += delegate { device.ActiveUntil = Native.Now() + 60; tick.Start(); };
                tick.Tick += delegate
                {
                    try
                    {
                        step++;
                        if (step == 1)
                        {
                            Check(device.Run.VisibleSeconds >= 1 && device.Run.ActiveSeconds >= 1, "live timer");
                            Check(manual.Run.PcSeconds >= 1 && !manual.Present, "manual live timer without device");
                            form.Show(); form.Close();
                            Check(!form.IsDisposed && !form.Visible, "close to tray");
                            beforeHide = device.Run.VisibleSeconds;
                        }
                        if (step < 4) return;
                        Check(device.Run.VisibleSeconds > beforeHide + 2, "background counting");
                        var saved = Storage.Load();
                        var savedDevice = saved.Devices.First(x => x.Id == device.Id);
                        var savedManual = saved.Devices.First(x => x.Id == manual.Id);
                        Check(savedDevice.Run.VisibleSeconds >= 4 && savedDevice.Enabled, "periodic durable save");
                        Check(savedDevice.ActiveUntil == 0 && !savedDevice.Present, "restart excludes process downtime");
                        Check(savedManual.Manual && savedManual.Enabled && savedManual.Run.PcSeconds >= 4 && savedManual.Run.ActiveSeconds == 0, "manual background save and restore");
                        File.WriteAllText(Storage.DataPath, "broken-json");
                        var recovered = Storage.Load();
                        Check(Storage.Warning != null && recovered.Devices.Count > 0, "backup recovery");
                        Storage.Warning = null;
                        tick.Stop();
                        data.History.Add(new TestRun { Name = "SC380 Pro", Manual = true, PcSeconds = 96422, Started = DateTime.UtcNow.AddDays(-4).ToString("o"), Ended = DateTime.UtcNow.ToString("o"), PollingRate = "1000", Notes = "2.4 GHz · RGB kapalı / off" });
                        form.CaptureDiagnostics(Path.Combine(Path.GetDirectoryName(output), "ui-preview"));
                        form.CheckNavigation();
                        File.WriteAllText(output, "PASS: live timer, close to tray, background counting, periodic save, restart state, backup recovery, manual live/background counting without device, manual save/restore, 12 tab transitions preserving the shell/sidebar, reselecting current tab does not rebuild, no hidden/duplicate content.\r\nIsolated test data: " + Storage.Folder, Encoding.UTF8);
                    }
                    catch (Exception ex) { File.WriteAllText(output, ex.ToString(), Encoding.UTF8); Environment.ExitCode = 1; }
                    if (step >= 4 || Environment.ExitCode != 0) { tick.Stop(); form.EndDiagnostics(); }
                };
                Application.Run(form);
            }
        }
        public static void Run(string[] args)
        {
            string output = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "PilTakip-selftest.txt");
            try
            {
                var d = new TrackedDevice { Id = "test", Name = "Test;\"device", Kind = "Mouse", Enabled = true, Present = true, Run = new TestRun(), ActiveUntil = 60 };
                Counter.Advance(d, 0, 30, false); Counter.Advance(d, 30, 90, false);
                Check(d.Run.VisibleSeconds == 90 && d.Run.ActiveSeconds == 60, "overlapping windows / idle cutoff");
                d.Enabled = false; Counter.Advance(d, 90, 100, false); Check(d.Run.VisibleSeconds == 90, "paused");
                d.Enabled = true; d.Present = false; Counter.Advance(d, 100, 200, false); Check(d.Run.VisibleSeconds == 90, "disconnected");
                d.Present = true; d.ActiveUntil = 400; Counter.Advance(d, 200, 220, true); Check(d.Run.VisibleSeconds == 110 && d.Run.ActiveSeconds == 60, "locked");
                Counter.Advance(d, 220, 210, false); Check(d.Run.VisibleSeconds == 110, "backwards clock");
                Check(MainForm.Duration(360005) == "100:00:05", "long duration");
                var state = new State(); d.Run.Name = d.Name; d.Run.DeviceId = d.Id; state.Devices.Add(d);
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(State)); serializer.WriteObject(stream, state); stream.Position = 0;
                    var read = (State)serializer.ReadObject(stream);
                    Check(read.Devices[0].Run.VisibleSeconds == 110 && read.Devices[0].Enabled && !read.Devices[0].Present && read.Devices[0].ActiveUntil == 0, "persisted counters / runtime reset");
                }
                state.History.Add(d.Run);
                Check(MainForm.ExportCsv(state).Contains("\"Test;\"\"device\""), "CSV escaping");
                var manual = state.AddManual("SC380 Pro");
                Check(!manual.Enabled && manual.Run.Manual, "manual begins paused");
                manual.Enabled = true;
                Counter.Advance(manual, 0, 60, false); Counter.Advance(manual, 60, 120, true);
                Check(manual.Run.PcSeconds == 120 && manual.Run.ActiveSeconds == 0 && manual.Run.VisibleSeconds == 0, "manual ignores device visibility, idle and lock");
                manual.Enabled = false; Counter.Advance(manual, 120, 180, false);
                Check(manual.Run.PcSeconds == 120, "manual pause");
                manual.Enabled = true; Counter.Advance(manual, 180, 190, false);
                Check(manual.Run.PcSeconds == 130, "manual resume");
                state.History.Add(manual.Run);
                Check(MainForm.ExportCsv(state).Contains("130;\"00:02:10\""), "manual CSV");
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(State)); serializer.WriteObject(stream, state); stream.Position = 0;
                    var read = (State)serializer.ReadObject(stream);
                    Check(read.Devices[0].Manual && read.Devices[0].Run.PcSeconds == 130 && read.Devices[0].Enabled, "manual persistence");
                }
                var found = Native.Enumerate();
                Check(Native.Now() > 0, "awake time clock");
                var journal = new State { Language = "EN", DashboardInitialized = true };
                var gear = journal.AddManual("SC380 Pro"); gear.Run.PcSeconds = 700; gear.Run.Notes = "2.4 GHz\nRGB off; \"quiet\""; gear.Run.PollingRate = "1000";
                Check(Records.Archive(journal, gear) && !gear.Enabled && journal.History.Count == 1, "archive freezes result");
                Check(!Records.Archive(journal, gear) && journal.History.Count == 1, "duplicate archive prevented");
                gear.Enabled = true; Counter.Advance(gear, 0, 20, false); Check(gear.Run.PcSeconds == 700, "archived result never accrues time");
                Records.Start(gear, 30); Check(gear.Enabled && gear.Run.PcSeconds == 0 && gear.Run.Ended == null && gear.Run.IdleSeconds == 30, "explicit fresh charge");
                Check(journal.History[0].PcSeconds == 700 && journal.History[0].PollingRate == "1000" && journal.History[0].Notes.Contains("2.4 GHz"), "new charge preserves archived metadata");
                journal.History[0].Notes += "\nUpdated note";
                using (var stream = new MemoryStream())
                {
                    var serializer = new DataContractJsonSerializer(typeof(State)); serializer.WriteObject(stream, journal); stream.Position = 0;
                    var read = (State)serializer.ReadObject(stream);
                    Check(read.Language == "EN" && read.Devices[0].Pinned && read.History[0].Notes.Contains("Updated note"), "language pins and editable notes persist");
                }
                Check(MainForm.ExportCsv(journal).Contains("\"quiet\"\"") && MainForm.ExportCsv(journal).Contains("Updated note"), "record notes CSV");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"Version\":1,\"IdleSeconds\":60,\"Devices\":[],\"History\":[],\"ManualInitialized\":true}")))
                {
                    var legacy = (State)new DataContractJsonSerializer(typeof(State)).ReadObject(stream);
                    Check(!legacy.DashboardInitialized && legacy.Devices.Count == 0, "legacy data compatibility");
                }
                File.WriteAllText(output, "PASS: 23 checks\r\nNative devices: " + found.Count + "\r\n" + string.Join("\r\n", found.Select(x => x.Name + " | " + x.Kind + " | " + x.Transport + " | " + x.Id)), Encoding.UTF8);
            }
            catch (Exception ex) { File.WriteAllText(output, ex.ToString(), Encoding.UTF8); Environment.ExitCode = 1; }
        }
    }
}
