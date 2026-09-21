// GPL-2.0-only. UI-independent companion runtime; preserves the feedback pipeline.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
namespace FarmMotion {
    internal struct EngineScopeFrame {
        public float Suspension,Acceleration,Force,Preview;
        public EngineScopeFrame(double suspension,double acceleration,double force,double preview) { Suspension=(float)suspension; Acceleration=(float)acceleration; Force=(float)force; Preview=(float)preview; }
        public float Channel(int index) { return index==0 ? Suspension:index==1 ? Acceleration:index==2 ? Force:Preview; }
    }
    internal sealed class CompanionEngine : IDisposable {
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
        readonly object gate=new object();
        readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Feel feel=new Feel();
        readonly bool testing;
        readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmMotion","settings.json");
        readonly Queue<EngineScopeFrame> scopeFrames=new Queue<EngineScopeFrame>(1000);
        FeelSettings settings=new FeelSettings(); AppOptions options=new AppOptions();
        Receiver receiver; ControllerOutput controller; Thread worker; volatile bool closing;
        Wheel.Info[] wheels=new Wheel.Info[0]; ControllerOutput.Info[] controllers=new ControllerOutput.Info[0];
        Guid selected; string controllerId="",deviceListKey="";
        bool armed,replaying,allowReplay,focused,wheelFault,clipped;
        FeedbackSolo solo=FeedbackSolo.All;
        double commanded,preview,suspension,acceleration,outputHz,outputGapMs,recordStart,replayStart;
        string vehicle="No active vehicle",issue=""; int replayIndex,wheelRefreshGeneration;
        List<RecordedSample> replay; RecordingWriter recorder;

        // Settings and Options are shared mutable objects. UI mutations must use
        // UpdateSettings / the routing methods, or hold Sync before ApplyOptions.
        public object Sync { get { return gate; } }
        public FeelSettings Settings { get { return settings; } }
        public AppOptions Options { get { return options; } }
        public bool Testing { get { return testing; } }
        public bool Armed { get { lock(gate) return armed; } set { lock(gate) armed=value; } }
        public bool Replaying { get { lock(gate) return replaying; } }
        public bool Recording { get { lock(gate) return recorder!=null; } }
        public bool AllowReplay { get { lock(gate) return allowReplay; } set { lock(gate) { allowReplay=value; armed=false; } } }
        public bool Focused { get { lock(gate) return focused; } set { lock(gate) focused=value; } }
        public FeedbackSolo Solo { get { lock(gate) return solo; } set { lock(gate) solo=value; } }
        public double Preview { get { lock(gate) return preview; } }
        public double Commanded { get { lock(gate) return commanded; } }
        public double Suspension { get { lock(gate) return suspension; } }
        public double Acceleration { get { lock(gate) return acceleration; } }
        public double OutputHz { get { lock(gate) return outputHz; } }
        public double OutputGapMs { get { lock(gate) return outputGapMs; } }
        public bool Clipped { get { lock(gate) return clipped; } }
        public string Vehicle { get { lock(gate) return vehicle; } }
        public string Issue { get { lock(gate) return issue; } set { lock(gate) issue=value ?? ""; } }
        public string SurfaceStatus { get { lock(gate) return feel.SurfaceStatus+(settings.RoadTexture==0 ? " • Road buzz off":""); } }
        public string ControllerStatus { get { return controller==null ? "Controller output idle":controller.Status; } }
        public Guid SelectedWheel { get { lock(gate) return selected; } }
        public string SelectedController { get { lock(gate) return controllerId; } }
        public Wheel.Info[] Wheels { get { lock(gate) return (Wheel.Info[])wheels.Clone(); } }
        public ControllerOutput.Info[] Controllers { get { lock(gate) return (ControllerOutput.Info[])controllers.Clone(); } }
        public string TelemetryStatus {
            get {
                if(receiver==null) return testing ? "Preview data • hardware output disabled":"Waiting for telemetry";
                return "Packets "+receiver.Packets+" • Invalid "+receiver.Invalid+" • Connection errors "+receiver.ConnectionErrors+" • Gaps "+receiver.Gaps+" • Last gap "+receiver.LastGap.ToString("0.00")+"s"+(clock.Elapsed.TotalSeconds-receiver.LastPacketTime>.25 ? " • Waiting for telemetry":"")+(receiver.RestartMarkerError=="" ? "":" • "+receiver.RestartMarkerError);
            }
        }
        public CompanionEngine(bool testing,bool startDisarmed=false) {
            this.testing=testing;
            if(testing) { UseDefaultProcessing(settings); settings.Validate(); return; }
            try { options=AppOptions.Load(); } catch(Exception e) { issue="App preferences could not be loaded: "+e.Message; startDisarmed=true; }
            if(!string.IsNullOrEmpty(AppOptions.LoadError)) { issue=AppOptions.LoadError+" Output is disabled; check device selections."; startDisarmed=true; }
            if(options.ControllerId.StartsWith("session:",StringComparison.Ordinal)) options.ControllerId="";
            try { settings=SettingsStore.Load(settingsPath); } catch { issue="Saved settings could not be loaded; using baseline."; }
            UseDefaultProcessing(settings); settings.Validate();
            receiver=new Receiver(clock); receiver.SampleReceived+=OnSample;
            controller=new ControllerOutput(); controller.SetBluetoothRumble(options.AllowBluetoothRumble);
            armed=options.EnableOutputOnStartup && !startDisarmed;
            if(startDisarmed && string.IsNullOrEmpty(AppOptions.LoadError) && issue=="") issue="Updated to v"+AppVersion.Display+". Enable output when ready.";
            worker=new Thread(OutputLoop) { IsBackground=true,Name="FarmMotion wheel" }; worker.SetApartmentState(ApartmentState.STA); worker.Start();
            // Discovery can involve driver calls; do not block the UI constructor.
            RefreshWheels();
        }
        internal static void UseDefaultProcessing(FeelSettings value) { value.Original=false; value.Enhanced=true; }
        public void UpdateSettings(Action<FeelSettings> update) {
            if(update==null) throw new ArgumentNullException("update");
            lock(gate) {
                bool original=settings.Original,enhanced=settings.Enhanced;
                update(settings); settings.Validate();
                if(original!=settings.Original || enhanced!=settings.Enhanced) { feel.Reset(); solo=FeedbackSolo.All; issue=settings.Enhanced && !settings.Original ? "V2: per-wheel bumps, layered texture, gradual peak compression.":"Previous processing restored. Sliders are shared between comparison modes."; }
            }
        }
        public void SetWheelEnabled(bool value) { lock(gate) { options.WheelEnabled=value; wheelFault=false; } }
        public void SetControllerEnabled(bool value) { lock(gate) options.ControllerEnabled=value; if(value && controller!=null) controller.Refresh(); }
        public void ApplyOptions() {
            bool bluetooth;
            lock(gate) { options.Validate(); bluetooth=options.AllowBluetoothRumble; }
            if(controller!=null) controller.SetBluetoothRumble(bluetooth);
        }
        public void SelectWheel(Guid id) { lock(gate) { selected=id; options.WheelId=id==Guid.Empty ? "":id.ToString(); wheelFault=false; } }
        public void SelectController(string id) { lock(gate) { controllerId=id ?? ""; options.ControllerId=controllerId.StartsWith("session:",StringComparison.Ordinal) ? "":controllerId; } }
        public void RefreshWheels() {
            if(testing || closing) return;
            int generation=BeginWheelRefresh();
            ThreadPool.QueueUserWorkItem(delegate {
                if(closing) return;
                Wheel.Info[] found; string failure=null;
                try { using(var wheel=new Wheel()) found=wheel.List().ToArray(); }
                catch(Exception e) { found=new Wheel.Info[0]; failure="Wheel discovery unavailable: "+e.Message; }
                ApplyWheelDiscovery(generation,found,failure);
            });
        }
        internal int BeginWheelRefresh() { lock(gate) return ++wheelRefreshGeneration; }
        internal void ApplyWheelDiscovery(int generation,Wheel.Info[] found,string failure) {
            lock(gate) {
                if(closing || generation!=wheelRefreshGeneration) return;
                PopulateWheels(found);
                if(failure!=null) issue=failure;
            }
        }
        public void RefreshControllers() { if(controller!=null && !closing) controller.Refresh(); }
        void PopulateWheels(Wheel.Info[] found) {
            lock(gate) {
                if(closing) return; wheels=found; selected=Guid.Empty;
                foreach(var item in wheels) if(item.Id.ToString()==options.WheelId) selected=item.Id;
                if(selected==Guid.Empty && string.IsNullOrEmpty(options.WheelId) && wheels.Length==1) { selected=wheels[0].Id; options.WheelId=selected.ToString(); }
            }
        }
        void PopulateControllers(ControllerOutput.Info[] found) {
            lock(gate) {
                var capable=new List<ControllerOutput.Info>(); string previous=controllerId; controllerId="";
                foreach(var item in found) if(item.SupportsRumble) { capable.Add(item); if(item.Id==options.ControllerId || item.Id==previous) controllerId=item.Id; }
                controllers=capable.ToArray();
                if(controllerId=="" && options.ControllerId=="" && controllers.Length==1 && !controllers[0].Id.StartsWith("session:",StringComparison.Ordinal)) { controllerId=controllers[0].Id; options.ControllerId=controllerId; }
            }
        }
        internal void SetTestDevices(Wheel.Info[] testWheels,ControllerOutput.Info[] testControllers) {
            if(!testing) throw new InvalidOperationException("Test devices require testing mode.");
            PopulateWheels(testWheels ?? new Wheel.Info[0]); PopulateControllers(testControllers ?? new ControllerOutput.Info[0]);
        }
        public void ToggleOutput() { bool retry; lock(gate) { retry=!armed; if(retry) { armed=true; wheelFault=false; issue=""; } else StopLocked(); } if(controller!=null) { if(retry) controller.Refresh(); else controller.Submit("",false,0,0); } }
        void StopLocked() { armed=false; replaying=false; commanded=preview=0; feel.Reset(); }
        public void Stop() { lock(gate) StopLocked(); if(controller!=null) controller.Submit("",false,0,0); }
        internal static bool OutputPermitted(bool armed,bool replay,bool consent,bool focused,bool gameFocused,bool fresh) { return armed && fresh && (replay ? consent && focused:gameFocused); }
        public void ReturnToLive() { lock(gate) { replaying=false; armed=false; feel.Reset(); } }
        public void SaveSettings() {
            if(testing) { Issue="Preview settings retained for this session."; return; }
            FeelSettings snapshot; AppOptions preferences;
            lock(gate) { snapshot=settings.Copy(); preferences=options.Copy(); }
            SettingsStore.Save(settingsPath,snapshot); preferences.Save();
            lock(gate) issue="Settings saved.";
        }
        void OnSample(Sample sample,string json,double now) {
            lock(gate) {
                if(closing) return;
                if(recorder!=null) try { recorder.WriteLine(FarmMotion.Recording.Line(now-recordStart,json)); } catch(Exception e) { issue="Recording stopped: "+e.Message; recorder.Dispose(); recorder=null; }
                if(!replaying) { feel.Push(sample,now,settings); vehicle=sample.Active ? "Vehicle "+sample.Vehicle:"No active vehicle / paused"; }
            }
        }
        public void Record(string path) {
            lock(gate) {
                if(testing) { issue="Recording is disabled in UI preview mode."; return; }
                if(recorder!=null) throw new InvalidOperationException("Already recording.");
                if(replaying) { issue="Return to live before recording."; return; }
                recorder=new RecordingWriter(path); recordStart=clock.Elapsed.TotalSeconds; issue="Recording live telemetry.";
            }
        }
        public void StopRecording() { lock(gate) { if(recorder==null) return; recorder.Dispose(); issue=recorder.Error==null ? "Recording saved.":"Recording stopped: "+recorder.Error; recorder=null; } }
        public void LoadReplay(string path) {
            lock(gate) armed=false;
            var loaded=FarmMotion.Recording.Load(path); lock(gate) replay=loaded; RestartReplay();
        }
        public void RestartReplay() {
            lock(gate) {
                if(replay==null) { issue="Load a recording first."; return; }
                if(recorder!=null) { recorder.Dispose(); recorder=null; }
                armed=false; replaying=true; replayIndex=0; replayStart=clock.Elapsed.TotalSeconds; feel.Reset(); issue="Replay loaded. Enable output separately to feel it.";
            }
        }
        public void Tick() {
            if(closing) return;
            if(controller!=null) {
                var found=controller.Devices; string key=""; foreach(var d in found) key+=d.Id+"|"+d.SupportsRumble+";";
                if(key!=deviceListKey) { deviceListKey=key; PopulateControllers(found); }
            }
            lock(gate) if(recorder!=null && recorder.Error!=null) { issue="Recording stopped: "+recorder.Error; recorder.Dispose(); recorder=null; }
        }
        public EngineScopeFrame[] DrainScopeFrames() { lock(gate) { var frames=scopeFrames.ToArray(); scopeFrames.Clear(); return frames; } }
        void OutputLoop() {
            Wheel wheel=null; Guid opened=Guid.Empty;
            bool preciseTimer=timeBeginPeriod(1)==0;
            double measuredStart=clock.Elapsed.TotalSeconds,lastUpdate=measuredStart,longest=0; int updates=0;
            try {
                while(!closing) {
                    bool enabled,canOutput,rumbleEnabled; Guid id; double force; string rumbleId; RumbleSignal rumble;
                    lock(gate) {
                        double now=clock.Elapsed.TotalSeconds;
                        if(replaying && replay!=null) {
                            while(replayIndex<replay.Count && replay[replayIndex].At<=now-replayStart) { var frame=replay[replayIndex++]; feel.Push(frame.Sample,replayStart+frame.At,settings); vehicle="Replay: "+frame.Sample.Vehicle; }
                            if(replayIndex==replay.Count && now-replayStart>replay[replay.Count-1].At+.15) { armed=false; feel.Reset(); issue="Replay finished. Replay again to compare settings."; }
                        }
                        preview=feel.Force(now,settings,solo); clipped=feel.Clipped; suspension=feel.Suspension; acceleration=feel.Acceleration;
                        canOutput=OutputPermitted(armed,replaying,allowReplay,focused,replaying ? false:Program.GameFocused(),feel.IsFresh(now));
                        enabled=armed && options.WheelEnabled && !wheelFault && selected!=Guid.Empty && canOutput; id=selected; force=enabled ? preview:0;
                        rumble=feel.GetRumble(now,settings,solo); rumble.Low*=options.ControllerStrength; rumble.High*=options.ControllerStrength;
                        rumbleEnabled=armed && options.ControllerEnabled && canOutput; rumbleId=controllerId;
                    }
                    if(controller!=null) controller.Submit(rumbleId,rumbleEnabled,rumble.Low,rumble.High);
                    try {
                        if(wheel!=null && opened!=id) { wheel.Dispose(); wheel=null; }
                        if(enabled && wheel==null) { wheel=new Wheel(); wheel.Open(id); opened=id; }
                        if(wheel!=null) { wheel.Write(force); if(!enabled) { wheel.Dispose(); wheel=null; } }
                        double sent=clock.Elapsed.TotalSeconds; longest=Math.Max(longest,sent-lastUpdate); lastUpdate=sent; updates++;
                        lock(gate) {
                            commanded=force; scopeFrames.Enqueue(new EngineScopeFrame(suspension/.45,acceleration/6,commanded/.1,preview/.1)); while(scopeFrames.Count>1000) scopeFrames.Dequeue();
                            if(sent-measuredStart>=1) { outputHz=updates/(sent-measuredStart); outputGapMs=longest*1000; measuredStart=sent; updates=0; longest=0; }
                        }
                    } catch(Exception e) { if(wheel!=null) { wheel.Dispose(); wheel=null; } lock(gate) { wheelFault=true; commanded=0; issue="Wheel output stopped: "+e.Message+". Toggle Wheel to retry; controller output is independent."; } }
                    Thread.Sleep(2);
                }
            } finally { if(controller!=null) controller.Submit("",false,0,0); if(wheel!=null) wheel.Dispose(); if(preciseTimer) timeEndPeriod(1); }
        }
        public void Dispose() {
            lock(gate) { if(closing) return; closing=true; armed=false; }
            if(receiver!=null) { receiver.SampleReceived-=OnSample; receiver.Dispose(); }
            if(worker!=null) worker.Join();
            if(controller!=null) controller.Dispose();
            lock(gate) { if(recorder!=null) { recorder.Dispose(); recorder=null; } }
        }
    }
}
