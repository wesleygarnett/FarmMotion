// Frozen pre-V2 processing used only for regression comparison. GPL-2.0-only.
using System;
namespace FarmMotion {
    internal sealed class FeelV1Reference {
        readonly Motion original=new Motion();
        Sample previous;
        double received=double.NegativeInfinity, lowWheel, slowWheel, lowBody, dcBody, texture, bump, body;
        public double Suspension, Acceleration;
        public bool Clipped;
        public void Reset() { previous=null; received=double.NegativeInfinity; lowWheel=slowWheel=lowBody=dcBody=texture=bump=body=Suspension=Acceleration=0; original.Reset(); Clipped=false; }
        static double Filter(double previous,double input,double hz,double dt) { return previous+(input-previous)*(1-Math.Exp(-2*Math.PI*hz*dt)); }
        public void Push(Sample s,double now,FeelSettings settings) {
            original.Sensitivity=settings.Sensitivity;
            original.Push(s,now);
            if(!s.Active) { Reset(); return; }
            var p=previous;
            if(p!=null && p.Session==s.Session && p.Vehicle==s.Vehicle && s.Seq<=p.Seq) return;
            double dt=p==null ? 0 : s.Time-p.Time;
            if(p==null || p.Session!=s.Session || p.Vehicle!=s.Vehicle || dt<.001 || dt>.15) {
                Reset(); previous=s; received=now; original.Push(s,now); return;
            }
            previous=s; received=now;
            // Preserve signed motion so compression and rebound form a single waveform.
            // RMS detail retains opposite-phase wheel movement that a signed mean can cancel.
            double sum=0, energy=0; int count=0;
            foreach(var w in s.Wheels) { double y; if(p.Wheels.TryGetValue(w.Key,out y)) { double rate=(w.Value-y)/dt; sum+=rate; energy+=rate*rate; count++; } }
            double velocity=count==0 ? 0 : sum/count;
            double rms=count==0 ? 0 : Math.Sqrt(energy/count);
            double accel=s.Vy.HasValue && p.Vy.HasValue ? (s.Vy.Value-p.Vy.Value)/dt : 0;
            double angular=0;
            if(s.Pitch.HasValue && p.Pitch.HasValue) angular+=(s.Pitch.Value-p.Pitch.Value)/dt;
            if(s.Roll.HasValue && p.Roll.HasValue) angular+=(s.Roll.Value-p.Roll.Value)/dt;
            if(rms>3 || Math.Abs(accel)>50 || Math.Abs(angular)>50) { Reset(); previous=s; received=now; return; }
            Suspension=velocity; Acceleration=accel;
            lowWheel=Filter(lowWheel,velocity,5,dt);
            slowWheel=Filter(slowWheel,velocity,.6,dt);
            bump=(lowWheel-slowWheel)/.45;
            // Faster residual movement controls the small texture component only.
            double fast=Math.Sqrt(Math.Max(0,rms*rms-lowWheel*lowWheel));
            texture=Filter(texture,Math.Max(0,fast-.025)/.45,8,dt);
            double bodyInput=accel/6+angular/12;
            lowBody=Filter(lowBody,bodyInput,2.5,dt); dcBody=Filter(dcBody,bodyInput,.4,dt);
            body=lowBody-dcBody;
        }
        public double Force(double now,FeelSettings s) {
            double age=now-received; Clipped=false;
            if(age<0 || age>=.15) return 0;
            if(s.Original) return original.Force(now,s.Strength);
            double fade=Math.Min(1,(.15-age)/.05);
            // Give small texture more headroom at low movement sensitivity without
            // amplifying the bump/body channels. Envelope still comes from telemetry.
            double detail=Math.Sqrt(s.Sensitivity)*s.Texture*texture*Math.Sin(2*Math.PI*s.TextureFrequency*now);
            double mixed=s.Sensitivity*(s.Bumps*bump+s.Body*body)+detail;
            Clipped=Math.Abs(mixed)>1;
            return s.Strength*Math.Max(-1,Math.Min(1,mixed))*fade;
        }
    }
}
