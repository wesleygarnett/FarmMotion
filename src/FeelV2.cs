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
        public bool IsFresh(double now) { return now>=received && now-received<.15; }
        public RumbleSignal GetRumble(double now,FeelSettings s,FeedbackSolo solo) {
            if(!IsFresh(now)) return new RumbleSignal();
            double fade=Math.Min(1,(.15-(now-received))/.05),low=0,high=0;
            if(FeedbackChannels.Includes(solo,FeedbackSolo.Bumps)) low+=Math.Abs(s.Sensitivity*s.Bumps*bump);
            if(FeedbackChannels.Includes(solo,FeedbackSolo.Body)) low+=Math.Abs(s.Sensitivity*s.Body*(bodyLow-bodySlow));
            if(FeedbackChannels.Includes(solo,FeedbackSolo.Texture)) high=Math.Sqrt(s.Sensitivity)*s.Texture*texture;
            return new RumbleSignal(ShapeMovement(low,s.ResponseCurve)*fade,Math.Min(high,s.TextureLimit)*fade);
        }
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
        // Preserve sign and full-scale impacts while reducing smaller movement.
        internal static double ShapeMovement(double x,double exponent) {
            return Math.Sign(x)*Math.Pow(Math.Min(1,Math.Abs(x)),exponent);
        }
        // Soft ceiling for continuous detail.
        internal static double Compress(double x,double ceiling) {
            return x/Math.Sqrt(1+(x/ceiling)*(x/ceiling));
        }
        public double Force(double now,FeelSettings s) { return Force(now,s,FeedbackSolo.All); }
        public double Force(double now,FeelSettings s,FeedbackSolo solo) {
            Limited=false; double age=now-received;
            if(age<0 || age>=.15) return 0;
            double fade=Math.Min(1,(.15-age)/.05);
            double movement=s.Sensitivity*(s.Bumps*bump+s.Body*(bodyLow-bodySlow));
            if(solo!=FeedbackSolo.All) movement=s.Sensitivity*((solo==FeedbackSolo.Bumps ? s.Bumps*bump:0)+(solo==FeedbackSolo.Body ? s.Body*(bodyLow-bodySlow):0));
            // Three non-harmonic components give texture variation rather than one
            // unchanging tone. Timing follows recorded game time for repeatable replays.
            double t=gameTime+age, f=s.TextureFrequency;
            double carrier=.45*Math.Sin(2*Math.PI*f*t)+.35*Math.Sin(2*Math.PI*f*.731*t+1.1)+.2*Math.Sin(2*Math.PI*f*.487*t+2.3);
            double detail=Math.Sqrt(s.Sensitivity)*s.Texture*texture*carrier;
            if(!FeedbackChannels.Includes(solo,FeedbackSolo.Texture)) detail=0;
            double shaped=ShapeMovement(movement,s.ResponseCurve);
            // Impacts own the full range; texture uses only remaining headroom.
            double detailCap=Math.Min(s.TextureLimit,Math.Max(0,1-Math.Abs(shaped)));
            double mixed=shaped+(detailCap>0 ? Compress(detail,detailCap):0);
            Limited=Math.Abs(movement)>1 || Math.Abs(detail)>detailCap;
            return s.Strength*Math.Max(-1,Math.Min(1,mixed))*fade;
        }
    }
}
