// GPL-2.0-only. Exercises runtime state transitions without opening devices.
using System;
using System.IO;
namespace FarmMotion {
    static class CompanionEngineTests {
        public static void Run(Action<bool,string> check) {
            using(var engine=new CompanionEngine(true)) {
                check(!engine.Armed && engine.Wheels.Length==0 && engine.Controllers.Length==0,"Engine preview starts disarmed without hardware");
                check(engine.Settings.Enhanced&&!engine.Settings.Original,"Engine starts with Motion-shaped V2");
                var legacy=new FeelSettings { Original=true,Enhanced=false,Strength=.09,Texture=1.13 }; CompanionEngine.UseDefaultProcessing(legacy);
                check(legacy.Enhanced&&!legacy.Original&&legacy.Strength==.09&&legacy.Texture==1.13,"Legacy mode upgrades to V2 without changing gains");
                engine.Solo=FeedbackSolo.Bumps; double gain=engine.Settings.Bumps;
                engine.UpdateSettings(delegate(FeelSettings s) { s.Enhanced=false; });
                check(engine.Solo==FeedbackSolo.All && engine.Settings.Bumps==gain,"Engine mode switch clears solo while retaining gain");
                engine.Armed=true; engine.AllowReplay=true;
                check(!engine.Armed && engine.AllowReplay,"Replay output consent never arms engine");
                engine.SetTestDevices(new Wheel.Info[0],new[]{new ControllerOutput.Info { Id="session:1",Name="Temporary",SupportsRumble=true }});
                check(engine.SelectedController=="","Engine never automatically selects a session-only controller");
                engine.SelectController("session:1");
                check(engine.Options.ControllerId=="" && engine.SelectedController=="session:1","Engine permits temporary selection without saving it");
                engine.SetTestDevices(new Wheel.Info[0],new ControllerOutput.Info[0]);
                engine.SetTestDevices(new Wheel.Info[0],new[]{new ControllerOutput.Info { Id="path:stable",Name="Stable",SupportsRumble=true }});
                check(engine.SelectedController=="path:stable" && engine.SelectedWheel==Guid.Empty && !engine.Armed,"Engine supports controller-only discovery without undoing STOP");
                engine.ToggleOutput(); engine.ToggleOutput();
                check(!engine.Armed && engine.Commanded==0 && engine.Preview==0,"Disable toggle clears output and preview");
                string replayPath=Path.GetTempFileName();
                try {
                    File.WriteAllLines(replayPath,new[]{Recording.Line(0,"{\"v\":1,\"active\":false}"),Recording.Line(.025,"{\"v\":1,\"active\":false}")});
                    engine.LoadReplay(replayPath); engine.AllowReplay=true; engine.ToggleOutput();
                    check(engine.Armed&&engine.Replaying,"Replay can be explicitly armed");
                    engine.ToggleOutput();
                    check(!engine.Armed&&!engine.Replaying&&engine.Commanded==0,"Disable uses STOP behavior during replay");
                } finally { File.Delete(replayPath); }
                check(!CompanionEngine.OutputPermitted(false,false,true,true,true,true)&&!CompanionEngine.OutputPermitted(false,true,true,true,true,true),"Disabled output blocks both live and replay routes");
                check(!CompanionEngine.OutputPermitted(true,false,true,true,false,true)&&!CompanionEngine.OutputPermitted(true,false,true,true,true,false),"Enable cannot bypass live focus or stale telemetry");
                check(!CompanionEngine.OutputPermitted(true,true,false,true,true,true)&&!CompanionEngine.OutputPermitted(true,true,true,false,true,true),"Enable cannot bypass replay consent or focus");
                check(CompanionEngine.OutputPermitted(true,false,false,false,true,true)&&CompanionEngine.OutputPermitted(true,true,true,true,false,true),"Eligible live and replay routes can output after enabling");
                engine.ToggleOutput(); engine.Stop();
                check(!engine.Armed && !engine.Replaying && engine.Commanded==0,"Engine STOP resets output and replay state");
                engine.Record("unused-preview-file.fmr"); engine.SaveSettings();
                check(!engine.Recording && engine.DrainScopeFrames().Length==0,"Engine preview cannot record or create hardware scope samples");
                int older=engine.BeginWheelRefresh(),newer=engine.BeginWheelRefresh();
                var latestWheel=new Wheel.Info { Id=Guid.NewGuid(),Name="Latest scan" };
                engine.ApplyWheelDiscovery(newer,new[]{latestWheel},null);
                engine.Issue="Newer user message";
                engine.ApplyWheelDiscovery(older,new Wheel.Info[0],"Stale discovery error");
                check(engine.Wheels.Length==1 && engine.Wheels[0].Id==latestWheel.Id && engine.Issue=="Newer user message","Older wheel scan cannot replace newer devices or errors");
            }
        }
    }
}
