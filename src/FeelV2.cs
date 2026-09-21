// GPL-2.0-only. Experimental per-wheel processing. V1 stays in Feel.cs.
using System;
using System.Collections.Generic;
namespace FarmMotion {
    public sealed class FeelV2 {
        sealed class WheelState { public double Low, Slow; }
        readonly Dictionary<int,WheelState> wheels=new Dictionary<int,WheelState>();
        Sample previous;
        double received=double.NegativeInfinity, gameTime, bump, texture, bodyLow, bodySlow;
        public bool Limited;
        public double Suspension,Acceleration;
        static double Filter(double old,double input,double hz,double dt) { return old+(input-old)*(1-Math.Exp(-2*Math.PI*hz*dt)); }
        public void Reset() { wheels.Clear(); previous=null; received=double.NegativeInfinity; gameTime=bump=texture=bodyLow=bodySlow=Suspension=Acceleration=0; Limited=false; }
        public void Push(Sample s,double now) {
            if(!s.Active) { Reset(); return; }
            var p=previous;
            if(p!=null && s.Session==p.Session && s.Vehicle==p.Vehicle && s.Seq<=p.Seq) return;
            double dt=p==null ? 0:s.Time-p.Time;
            if(p==null || p.Session!=s.Session || p.Vehicle!=s.Vehicle || dt<.001 || dt>.15) { Reset(); previous=s; received=now; gameTime=s.Time; return; }
            double accel=s.Vy.HasValue && p.Vy.HasValue ? (s.Vy.Value-p.Vy.Value)/dt : 0;
            double pitch=s.Pitch.HasValue && p.Pitch.HasValue ? (s.Pitch.Value-p.Pitch.Value)/dt : 0;
            double roll=s.Roll.HasValue && p.Roll.HasValue ? (s.Roll.Value-p.Roll.Value)/dt : 0;
            var rates=new SortedDictionary<int,double>();
            bool discontinuity=Math.Abs(accel)>50 || Math.Abs(pitch)>50 || Math.Abs(roll)>50;
            foreach(var w in s.Wheels) { double y; if(p.Wheels.TryGetValue(w.Key,out y)) { double rate=(w.Value-y)/dt; rates[w.Key]=rate; discontinuity |= Math.Abs(rate)>3; } }
            if(discontinuity) { Reset(); previous=s; received=now; gameTime=s.Time; return; }
            previous=s; received=now; gameTime=s.Time;
            foreach(int id in new List<int>(wheels.Keys)) if(!rates.ContainsKey(id)) wheels.Remove(id);
            double mean=0,dominant=0,fastEnergy=0,raw=0;
            foreach(var pair in rates) {
                WheelState state;
                if(!wheels.TryGetValue(pair.Key,out state)) { state=new WheelState(); wheels[pair.Key]=state; }
                state.Low=Filter(state.Low,pair.Value,8,dt);
                state.Slow=Filter(state.Slow,pair.Value,.6,dt);
                double band=state.Low-state.Slow;
                mean+=band; if(Math.Abs(band)>Math.Abs(dominant)) dominant=band;
                // Remove slow motion from EACH wheel before taking RMS: opposing
                // slow wheel movements must not become false high-frequency texture.
                double fast=pair.Value-state.Low; fastEnergy+=fast*fast; raw+=pair.Value;
            }
            int count=rates.Count;
            if(count>0) { mean/=count; fastEnergy/=count; raw/=count; }
            // Retain common motion plus a modest strongest-wheel contribution, so
            // opposite-phase wheels cannot completely erase a bump. This is a tactile
            // mapping, not a claimed steering-rack model or inferred steering axle.
            bump=Filter(bump,(mean+.5*dominant)/1.5/.45,12,dt);
            double target=Math.Max(0,Math.Sqrt(fastEnergy)-.008)/.25;
            texture=Filter(texture,target,target>texture ? 15:6,dt);
            double bodyInput=accel/6+pitch/12+roll/12;
            bodyLow=Filter(bodyLow,bodyInput,2.5,dt); bodySlow=Filter(bodySlow,bodyInput,.4,dt);
            Suspension=raw; Acceleration=accel;
        }
        // Unity slope for small signals, smoothly compressing peaks without hard clipping.
        internal static double Compress(double x,double ceiling) {
            return x/Math.Sqrt(1+(x/ceiling)*(x/ceiling));
        }
        public double Force(double now,FeelSettings s) {
            Limited=false; double age=now-received;
            if(age<0 || age>=.15) return 0;
            double fade=Math.Min(1,(.15-age)/.05);
            double movement=s.Sensitivity*(s.Bumps*bump+s.Body*(bodyLow-bodySlow));
            // Three non-harmonic components give texture variation rather than one
            // unchanging tone. Timing follows recorded game time for repeatable replays.
            double t=gameTime+age, f=s.TextureFrequency;
            double carrier=.45*Math.Sin(2*Math.PI*f*t)+.35*Math.Sin(2*Math.PI*f*.731*t+1.1)+.2*Math.Sin(2*Math.PI*f*.487*t+2.3);
            double detail=Math.Sqrt(s.Sensitivity)*s.Texture*texture*carrier;
            bool hasDetail=s.Texture>0 && texture>.001;
            double baseCap=hasDetail ? .80:1;
            double mixed=Compress(movement,baseCap)+(hasDetail ? Compress(detail,.20):0);
            Limited=Math.Abs(movement)>baseCap || (hasDetail && Math.Abs(detail)>.20);
            return s.Strength*Math.Max(-1,Math.Min(1,mixed))*fade;
        }
    }
}
