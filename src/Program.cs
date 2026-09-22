// GPL-2.0-only. See LICENSE.
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FarmMotion {
    sealed class Receiver : IDisposable {
        readonly object gate=new object(); readonly Stopwatch clock;
        readonly string pipeName, discoveryName;
        TelemetryDiscovery discovery;
        public string RestartMarkerError { get { return discovery==null ? "":discovery.Error; } }
        NamedPipeServerStream pipe;
        volatile bool done; public readonly Motion Motion=new Motion();
        public int Packets, Invalid, ConnectionErrors;
        public int Gaps;
        public double LastPacketTime=double.NegativeInfinity, LastGap;
        public string LastError = "none";
        public event Action<Sample,string,double> SampleReceived;
        public Receiver(Stopwatch timer, string name="FarmMotionTelemetry", string restartDiscoveryName=null) { clock=timer; pipeName=name; discoveryName=restartDiscoveryName ?? name+"Session"; new Thread(Listen) { IsBackground=true }.Start(); }
        internal static PipeSecurity LocalPipeSecurity() {
            var security=new PipeSecurity(); security.SetAccessRuleProtection(true,false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
            using(var identity=WindowsIdentity.GetCurrent()) security.AddAccessRule(new PipeAccessRule(identity.User,PipeAccessRights.FullControl,AccessControlType.Allow));
            return security;
        }
        void Listen() {
            while(!done) {
                try {
                    using(var incoming=new NamedPipeServerStream(pipeName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None,16384,0,LocalPipeSecurity())) {
                        lock(gate) { if(done) return; pipe=incoming; }
                        lock(gate) { if(done) return; if(discovery==null) discovery=new TelemetryDiscovery(discoveryName); }
                        incoming.WaitForConnection();
                        using(var reader=new StreamReader(incoming,Encoding.UTF8,false,4096)) {
                            var line=new StringBuilder(); int c;
                            while(!done && (c=reader.Read())>=0) {
                                if(c=='\n') {
                                    string json=line.ToString(); Sample sample=null; double now=0; Action<Sample,string,double> received=null;
                                    lock(gate) {
                                        try { sample=Sample.Parse(json); now=clock.Elapsed.TotalSeconds; if(Packets>0 && now-LastPacketTime>.25) { Gaps++; LastGap=now-LastPacketTime; } LastPacketTime=now; Motion.Push(sample,now); Packets++; received=SampleReceived; }
                                        catch(Exception e) { Invalid++; LastError="Packet: "+e.Message; Motion.Reset(); }
                                    }
                                    // Consumers can perform storage or UI work. Keep them outside the
                                    // receiver lock, and do not misclassify their failures as bad telemetry.
                                    if(sample!=null && received!=null) try { received(sample,json,now); } catch(Exception e) { lock(gate) LastError="Telemetry consumer: "+e.Message; }
                                    line.Length=0;
                                } else { line.Append((char)c); if(line.Length>16384) throw new IOException("Packet too long"); }
                            }
                        }
                    }
                } catch(Exception e) { if(!done) { lock(gate) { ConnectionErrors++; LastError=e.GetType().Name+": "+e.Message; } Thread.Sleep(250); } }
                // A disconnected producer must not leave a held vibration behind.
                lock(gate) { Motion.Reset(); pipe=null; }
            }
        }
        public double Force(double now,double strength) { lock(gate) return Motion.Force(now,strength); }
        public double Level(double now) { lock(gate) return Motion.Level(now); }
        public void SetSensitivity(double value) { lock(gate) Motion.Sensitivity=value; }
        public string Status(double now) { lock(gate) return string.Format("Packets: {0}  Invalid: {1}  Connection errors: {2}  Movement: {3:0.000}  Last error: {4}",Packets,Invalid,ConnectionErrors,Motion.Level(now),LastError); }
        public void Dispose() { lock(gate) { done=true; if(discovery!=null) discovery.Dispose(); if(pipe!=null) pipe.Dispose(); Motion.Reset(); } }
    }
    static class Program {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
        static uint cachedPid; static bool cachedGame; static long checkedAt;
        internal static bool GameFocused() {
            try {
                uint pid; GetWindowThreadProcessId(GetForegroundWindow(),out pid);
                long now=Stopwatch.GetTimestamp();
                if(pid!=cachedPid || now-checkedAt>Stopwatch.Frequency) {
                    cachedPid=pid; checkedAt=now; cachedGame=false;
                    using(var process=Process.GetProcessById((int)pid)) cachedGame=process.ProcessName.StartsWith("FarmingSimulator2025",StringComparison.OrdinalIgnoreCase);
                }
                return cachedGame;
            } catch { cachedGame=false; return false; }
        }
        [STAThread] static int Main(string[] args) {
            try {
                if(args.Length>0 && args[0]=="--apply-update") return AppUpdate.Apply(args);
                if(Array.IndexOf(args,"--self-test")>=0) return Tests.Run();
                if(args.Length==0 || Array.IndexOf(args,"--gui")>=0 || Array.IndexOf(args,"--startup")>=0 || Array.IndexOf(args,"--after-update")>=0 || Array.IndexOf(args,"--ui-test")>=0) {
                    bool owns; int uiExit=0;
                    using(var mutex=new Mutex(true,Array.IndexOf(args,"--ui-test")>=0 ? "Local\\FarmMotionUITest" : "Local\\FarmMotionCompanion",out owns)) {
                        if(!owns) { if(Array.IndexOf(args,"--startup")>=0) return 0; throw new InvalidOperationException("FarmMotion is already running."); }
                        try {
                            var application=new System.Windows.Application();
                            application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ThemesDictionary { Theme=Wpf.Ui.Appearance.ApplicationTheme.Dark });
                            application.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                            uiExit=application.Run(new WpfDashboard(Array.IndexOf(args,"--ui-test")>=0,Array.IndexOf(args,"--after-update")>=0));
                        }
                        finally { mutex.ReleaseMutex(); }
                    }
                    return uiExit;
                }
                bool enable=false, list=false; Guid id=Guid.Empty; double strength=FeelSettings.DefaultStrength, sensitivity=1; string log=null;
                for(int i=0;i<args.Length;i++) {
                    if(args[i]=="--enable") enable=true;
                    else if(args[i]=="--monitor") { }
                    else if(args[i]=="--list-wheels") list=true;
                    else if(args[i]=="--wheel" && i+1<args.Length) id=Guid.Parse(args[++i]);
                    else if(args[i]=="--strength" && i+1<args.Length) strength=double.Parse(args[++i],CultureInfo.InvariantCulture);
                    else if(args[i]=="--sensitivity" && i+1<args.Length) sensitivity=double.Parse(args[++i],CultureInfo.InvariantCulture);
                    else if(args[i]=="--log" && i+1<args.Length) log=args[++i];
                    else throw new ArgumentException("Usage: FarmMotion.exe [--list-wheels | --self-test | --enable --wheel GUID --strength 0.03]");
                }
                if(double.IsNaN(strength)||double.IsInfinity(strength)||strength<0||strength>FeelSettings.MaximumStrength) throw new ArgumentException("Strength must be between 0 and 1.0");
                if(double.IsNaN(sensitivity)||double.IsInfinity(sensitivity)||sensitivity<0.25||sensitivity>3) throw new ArgumentException("Sensitivity must be between 0.25 and 3");
                if(list) { using(var w=new Wheel()) { var devices=w.List(); foreach(var d in devices) Console.WriteLine(d); if(devices.Count==0) Console.WriteLine("No attached force-feedback devices found."); } return 0; }
                bool owned;
                using(var mutex=new Mutex(true,"Local\\FarmMotionCompanion",out owned)) {
                    if(!owned) throw new InvalidOperationException("FarmMotion is already running.");
                    Wheel wheel=null;
                    try {
                        if(enable) {
                            if(id==Guid.Empty) throw new ArgumentException("Choose a wheel GUID using --list-wheels first.");
                            wheel=new Wheel(); var found=wheel.List().Find(d=>d.Id==id);
                            if(found==null) throw new ArgumentException("Selected wheel is not attached.");
                            Console.WriteLine("Enabling "+found.Name+" at "+(strength*100).ToString("0.#")+"% maximum. Output only while FS25 is foreground.");
                            wheel.Open(id);
                        }
                        var timer=Stopwatch.StartNew(); bool stop=false;
                        Console.CancelKeyPress += delegate(object sender,ConsoleCancelEventArgs e) { e.Cancel=true; stop=true; };
                        Console.WriteLine(enable ? "Armed. Escape or Ctrl+C stops output." : "DIAGNOSTIC MODE: no wheel forces. Waiting for FS25 telemetry. Escape or Ctrl+C exits.");
                        Console.WriteLine("In this window: +/- strength (0.5% steps); [/] sensitivity (0.25 steps). Switch back to FS25 to feel changes.");
                        using(var logger=log==null ? null : new StreamWriter(log,false) { AutoFlush=true })
                        using(var receiver=new Receiver(timer)) {
                            receiver.SetSensitivity(sensitivity);
                            double report=0;
                            while(!stop) {
                                if(!Console.IsInputRedirected && Console.KeyAvailable) {
                                    var key=Console.ReadKey(true);
                                    if(key.Key==ConsoleKey.Escape) break;
                                    if(key.KeyChar=='+' || key.KeyChar=='=') strength=Math.Min(FeelSettings.MaximumStrength,strength+.005);
                                    if(key.KeyChar=='-') strength=Math.Max(0,strength-.005);
                                    if(key.KeyChar=='[') sensitivity=Math.Max(.25,sensitivity-.25);
                                    if(key.KeyChar==']') sensitivity=Math.Min(3,sensitivity+.25);
                                    receiver.SetSensitivity(sensitivity);
                                }
                                double now=timer.Elapsed.TotalSeconds;
                                if(wheel!=null) wheel.Write(GameFocused() ? receiver.Force(now,strength) : 0);
                                if(now>=report) { string status=receiver.Status(now)+string.Format("  Strength: {0:0.0}%  Sensitivity: {1:0.00}x",strength*100,sensitivity); Console.WriteLine(status); if(logger!=null) logger.WriteLine(DateTime.Now.ToString("s")+" "+status); report=now+1; }
                                Thread.Sleep(5);
                            }
                        }
                    } finally { if(wheel!=null) wheel.Dispose(); mutex.ReleaseMutex(); }
                }
                return 0;
            } catch(Exception e) { Console.Error.WriteLine(e.Message); if(args.Length>0&&args[0]=="--apply-update") System.Windows.Forms.MessageBox.Show("FarmMotion update failed: "+e.Message+"\r\nRestart your existing FarmMotion copy. A backup is retained in the application folder if files were replaced.","FarmMotion update",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Error); return 1; }
        }
    }
}
