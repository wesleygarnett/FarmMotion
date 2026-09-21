// GPL-2.0-only. SDL3 ordinary two-channel rumble, independent of wheel acquisition.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FarmMotion {
    public sealed class ControllerOutput : IDisposable {
        public sealed class Info {
            public string Id { get; internal set; }
            public string Name { get; internal set; }
            public bool SupportsRumble { get; internal set; }
            public override string ToString() { return Name+(SupportsRumble ? "" : " (rumble unavailable)"); }
        }
        sealed class Device { public IntPtr Handle; public Info Info; }
        readonly object gate=new object();
        readonly Thread worker;
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly AutoResetEvent wake=new AutoResetEvent(false);
        Info[] devices=new Info[0];
        string status="Finding controllers", selected="";
        bool closing, enabled, refresh=true, bluetooth;
        double low,high,submitted=double.NegativeInfinity;
        public Info[] Devices { get { lock(gate) return (Info[])devices.Clone(); } }
        public string Status { get { lock(gate) return status; } }
        public ControllerOutput() { worker=new Thread(Run) { IsBackground=true,Name="FarmMotion rumble" }; worker.Start(); }
        public void Refresh() { lock(gate) { if(closing) return; refresh=true; wake.Set(); } }
        // Opting in can change PlayStation Bluetooth report mode until a power cycle.
        public void SetBluetoothRumble(bool allowed) { lock(gate) { if(closing) return; bluetooth=allowed; refresh=true; wake.Set(); } }
        public void Submit(string deviceId,bool outputEnabled,double lowFrequency,double highFrequency) {
            lock(gate) { if(closing) return; selected=deviceId ?? ""; enabled=outputEnabled; low=Clamp(lowFrequency); high=Clamp(highFrequency); submitted=clock.Elapsed.TotalSeconds; }
        }
        internal static double Clamp(double value) { return double.IsNaN(value)||double.IsInfinity(value) ? 0:Math.Max(0,Math.Min(1,value)); }
        internal static ushort Motor(double value) { return (ushort)Math.Round(Clamp(value)*65535); }
        internal static bool Fresh(double now,double at) { return now>=at && now-at<.15; }
        internal static double Smooth(double old,double target,double dt) { target=Clamp(target); return Clamp(old+(target-old)*(1-Math.Exp(-Math.Max(0,Math.Min(.1,dt))/(target>old ? .015:.060)))); }
        internal static string Identity(ushort vendor,ushort product,string serial,string path,uint instance) {
            if(!string.IsNullOrEmpty(serial)) return "serial:"+vendor.ToString("X4")+":"+product.ToString("X4")+":"+serial;
            if(!string.IsNullOrEmpty(path)) return "path:"+path;
            return "session:"+instance;
        }
        void SetStatus(string value) { lock(gate) status=value; }
        static string Utf8(IntPtr p) { if(p==IntPtr.Zero) return ""; int n=0; while(Marshal.ReadByte(p,n)!=0 && n<32768) n++; var bytes=new byte[n]; Marshal.Copy(p,bytes,0,n); return Encoding.UTF8.GetString(bytes); }
        static void Stop(Device d) { if(d!=null) Native.SDL_RumbleJoystick(d.Handle,0,0,0); }
        void Scan(Dictionary<uint,Device> known) {
            int count; IntPtr ids=Native.SDL_GetJoysticks(out count);
            if(ids==IntPtr.Zero) throw new InvalidOperationException(Utf8(Native.SDL_GetError()));
            var present=new HashSet<uint>();
            try {
                for(int i=0;i<count;i++) {
                    uint id=unchecked((uint)Marshal.ReadInt32(ids,i*4));
                    int type=Native.SDL_GetJoystickTypeForID(id);
                    if(type!=0 && type!=1) continue; // Never open identified wheels through SDL.
                    present.Add(id);
                    if(known.ContainsKey(id)) {
                        Device existing=known[id];
                        existing.Info=new Info { Id=existing.Info.Id,Name=existing.Info.Name,SupportsRumble=Native.SDL_GetBooleanProperty(Native.SDL_GetJoystickProperties(existing.Handle),"SDL.joystick.cap.rumble",false) };
                        continue;
                    }
                    IntPtr handle=Native.SDL_OpenJoystick(id); if(handle==IntPtr.Zero) continue;
                    string name=Utf8(Native.SDL_GetJoystickNameForID(id));
                    var info=new Info { Id=Identity(Native.SDL_GetJoystickVendorForID(id),Native.SDL_GetJoystickProductForID(id),Utf8(Native.SDL_GetJoystickSerial(handle)),Utf8(Native.SDL_GetJoystickPathForID(id)),id),Name=string.IsNullOrEmpty(name) ? "Controller":name,SupportsRumble=Native.SDL_GetBooleanProperty(Native.SDL_GetJoystickProperties(handle),"SDL.joystick.cap.rumble",false) };
                    known.Add(id,new Device { Handle=handle,Info=info });
                }
            } finally { Native.SDL_free(ids); }
            foreach(uint id in new List<uint>(known.Keys)) if(!present.Contains(id)) { Native.SDL_CloseJoystick(known[id].Handle); known.Remove(id); }
            var result=new List<Info>(); foreach(var d in known.Values) result.Add(d.Info);
            lock(gate) devices=result.ToArray();
        }
        void Run() {
            var known=new Dictionary<uint,Device>(); Device active=null;
            bool initialized=false; double nextScan=0,lastTick=clock.Elapsed.TotalSeconds,filteredLow=0,filteredHigh=0; bool lastBluetooth=false;
            Device faulted=null; string faultMessage="",lastSelection="";
            try {
                Native.SDL_SetMainReady();
                Native.SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS","0");
                Native.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");
                if(!Native.SDL_InitSubSystem(0x200)) throw new InvalidOperationException(Utf8(Native.SDL_GetError()));
                initialized=true; Native.SDL_SetJoystickEventsEnabled(false);
                while(true) {
                    string id; bool on,rescan,allowBluetooth; double l,h,at;
                    lock(gate) { if(closing) break; id=selected; on=enabled; l=low; h=high; at=submitted; rescan=refresh; refresh=false; allowBluetooth=bluetooth; }
                    // enabled includes focus/telemetry gating. Only an explicit refresh or
                    // selection change clears errors; focus return must not retry a fault.
                    if(rescan || id!=lastSelection) { faulted=null; faultMessage=""; }
                    lastSelection=id;
                    if(allowBluetooth!=lastBluetooth) {
                        Stop(active); active=null; filteredLow=filteredHigh=0;
                        foreach(var d in known.Values) Native.SDL_CloseJoystick(d.Handle);
                        known.Clear();
                        Native.SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS",allowBluetooth ? "auto":"0"); lastBluetooth=allowBluetooth; rescan=true;
                    }
                    Native.SDL_UpdateJoysticks();
                    double now=clock.Elapsed.TotalSeconds;
                    double dt=now-lastTick; lastTick=now;
                    if(rescan || now>=nextScan) { Scan(known); nextScan=now+1; }
                    Device target=null; int matches=0;
                    foreach(var d in known.Values) if(d.Info.Id==id) { target=d; matches++; }
                    if(matches!=1) target=null; // Ambiguous IDs must never target an arbitrary controller.
                    if(active!=target) { if(active!=null && known.ContainsValue(active)) Stop(active); active=null; filteredLow=filteredHigh=0; }
                    bool permitted=on && target!=null && target.Info.SupportsRumble && Fresh(now,at) && target!=faulted;
                    if(!permitted) { if(active!=null) Stop(active); active=null; filteredLow=filteredHigh=0; SetStatus(!on ? "Controller output off":target==null ? "Selected controller disconnected":target==faulted ? faultMessage:!target.Info.SupportsRumble ? "Controller rumble unavailable":"Controller waiting for feedback"); }
                    else {
                        active=target;
                        filteredLow=Smooth(filteredLow,l,dt); filteredHigh=Smooth(filteredHigh,h,dt);
                        if(!Native.SDL_RumbleJoystick(target.Handle,Motor(filteredLow),Motor(filteredHigh),100)) { faultMessage="Controller stopped: "+Utf8(Native.SDL_GetError())+". Refresh controllers to retry."; faulted=target; Stop(active); active=null; filteredLow=filteredHigh=0; SetStatus(faultMessage); }
                        else SetStatus("Rumble: "+target.Info.Name);
                    }
                    wake.WaitOne(20);
                }
            } catch(Exception e) { SetStatus("Controller unavailable: "+e.Message); }
            finally {
                if(initialized) {
                    if(active!=null && known.ContainsValue(active)) Stop(active);
                    foreach(var d in known.Values) Native.SDL_CloseJoystick(d.Handle);
                    Native.SDL_QuitSubSystem(0x200);
                }
                lock(gate) devices=new Info[0];
            }
        }
        public void Dispose() { lock(gate) { if(closing) return; closing=true; wake.Set(); } worker.Join(); wake.Dispose(); }
        static class Native {
            const string Dll="SDL3.dll";
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_SetMainReady();
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_InitSubSystem(uint flags);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_QuitSubSystem(uint flags);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_SetHint(string name,string value);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetError();
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetJoysticks(out int count);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_free(IntPtr pointer);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern int SDL_GetJoystickTypeForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern ushort SDL_GetJoystickVendorForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern ushort SDL_GetJoystickProductForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetJoystickNameForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetJoystickPathForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetJoystickSerial(IntPtr joystick);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern IntPtr SDL_OpenJoystick(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_CloseJoystick(IntPtr joystick);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern uint SDL_GetJoystickProperties(IntPtr joystick);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_GetBooleanProperty(uint properties,string name,[MarshalAs(UnmanagedType.I1)] bool fallback);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_UpdateJoysticks();
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] internal static extern void SDL_SetJoystickEventsEnabled([MarshalAs(UnmanagedType.I1)] bool enabled);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool SDL_RumbleJoystick(IntPtr joystick,ushort low,ushort high,uint duration);
        }
    }
}
