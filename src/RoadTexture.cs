// GPL-2.0-only. Optional synthetic tractor-tread buzz; the motion signal is untouched.
using System;
using System.Collections.Generic;
namespace FarmMotion {
    public sealed class RoadTexture {
        sealed class State { public double Level; }
        readonly Dictionary<int,State> wheels=new Dictionary<int,State>();
        Sample previous;
        double received=double.NegativeInfinity;
        double phase,hz=75;
        public string Status="Surface data: waiting for mod 0.2.0";
        public void Reset() { previous=null; wheels.Clear(); received=double.NegativeInfinity; phase=0; hz=75; Status="Surface data: waiting"; }
        public void Push(Sample s,double now,FeelSettings settings) {
            if(!s.Active) { Reset(); Status="Surface: inactive"; return; }
            if(!s.HasSurfaceData) { Reset(); Status="Road buzz needs mod 0.2.0"; return; }
            var p=previous;
            if(p!=null && s.Session==p.Session && s.Vehicle==p.Vehicle && s.Seq<=p.Seq) return;
            double dt=p==null ? 0:s.Time-p.Time;
            if(p==null || p.Session!=s.Session || p.Vehicle!=s.Vehicle || dt<.001 || dt>.15) { Reset(); dt=0; }
            // One continuous carrier avoids slow beating between nearly equal tyre speeds.
            // Use the output clock, not game time: packet arrival jitter must not reset phase.
            if(!double.IsNegativeInfinity(received)) phase=(phase+2*Math.PI*hz*Math.Max(0,now-received))%(2*Math.PI);
            hz=settings.RoadFrequency;
            previous=s; received=now;
            foreach(int id in new List<int>(wheels.Keys)) if(!s.Tyres.ContainsKey(id)) wheels.Remove(id);
            int road=0,eligible=0;
            foreach(var pair in s.Tyres) {
                var tyre=pair.Value; State state;
                if(!wheels.TryGetValue(pair.Key,out state)) { state=new State(); wheels[pair.Key]=state; }
                bool roadContact=tyre.Contact && tyre.Surface=="road";
                if(roadContact) road++;
                string type=(tyre.Tire ?? "").ToLowerInvariant();
                bool tread=type=="mud" || type=="offroad";
                double omega=tyre.Omega.HasValue ? Math.Abs(tyre.Omega.Value):0;
                bool moving=s.Speed.HasValue && s.Speed.Value>.15 && omega>.05 && omega<1000;
                double target=0;
                if(roadContact && tread && moving) {
                    // Retain the rolling-speed envelope. Pitch is now independent, so
                    // slow wheel rotation fades the buzz instead of creating a low thump.
                    double rawHz=omega/(2*Math.PI)*settings.TreadCount;
                    target=Math.Min(1,Math.Max(0,(rawHz-8)/14))*Math.Min(1,(s.Speed.Value-.15)/1.5);
                    eligible++;
                }
                // Fast quieting on lost contact/stopping, gentle surface transitions.
                double tau=!tyre.Contact || !moving ? .025:.12;
                state.Level += (target-state.Level)*(1-Math.Exp(-dt/tau));
            }
            Status=road==0 ? "Surface: no road-class contact" : string.Format("Road wheels: {0} | tread wheels: {1} | buzz {2:0} Hz",road,eligible,hz);
        }
        public double Force(double now,FeelSettings settings) {
            double age=now-received;
            if(settings.RoadTexture<=0 || age<0 || age>=.15 || wheels.Count==0) return 0;
            double value=0;
            foreach(var state in wheels.Values) value+=state.Level;
            value/=wheels.Count;
            // A quarter of the previous per-tyre ceiling; keep the layer a fine detail.
            return value*Math.Sin(phase+2*Math.PI*hz*age)*settings.Strength*settings.RoadTexture*.25*Math.Min(1,(.15-age)/.05);
        }
        public static double Mix(double motion,double detail,double strength) {
            // Preserve large bumps exactly; sacrifice buzz, never rescale the base force.
            double room=Math.Max(0,strength-Math.Abs(motion));
            return motion+Math.Max(-room,Math.Min(room,detail));
        }
    }
}
