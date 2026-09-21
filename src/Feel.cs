// GPL-2.0-only. Motion-shaped feedback; no terrain identification or periodic bump generator.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;
namespace FarmMotion {
    public sealed class FeelSettings {
        public double Strength=.07, Sensitivity=.25, Texture=.20, Bumps=1, Body=.35;
        public double TextureFrequency=28;
        public double RoadTexture=0, TreadCount=40;
        public double RoadFrequency=75;
        public bool Original=false;
        // Missing in older settings files: preserve the existing V1 selection.
        public bool Enhanced=false;
        public void Validate() {
            Strength=Limit(Strength,0,.1,.07); Sensitivity=Limit(Sensitivity,.25,3,.25);
            Texture=Limit(Texture,0,4,.2); Bumps=Limit(Bumps,0,2,1); Body=Limit(Body,0,2,.35);
            TextureFrequency=Limit(TextureFrequency,12,45,28);
            RoadTexture=Limit(RoadTexture,0,.2,0); TreadCount=Limit(TreadCount,20,80,40);
            RoadFrequency=Limit(RoadFrequency,50,90,75);
        }
        static double Limit(double n,double lo,double hi,double fallback) { return double.IsNaN(n)||double.IsInfinity(n) ? fallback : Math.Max(lo,Math.Min(hi,n)); }
        public FeelSettings Copy() { return (FeelSettings)MemberwiseClone(); }
    }
    public sealed class Feel {
        readonly Motion original=new Motion();
        readonly FeelV2 enhanced=new FeelV2();
        readonly RoadTexture road=new RoadTexture();
        public string SurfaceStatus { get { return road.Status; } }
        Sample previous;
        double received=double.NegativeInfinity, lowWheel, slowWheel, lowBody, dcBody, texture, bump, body;
        public double Suspension, Acceleration;
        public bool Clipped;
        public void Reset() { previous=null; received=double.NegativeInfinity; lowWheel=slowWheel=lowBody=dcBody=texture=bump=body=Suspension=Acceleration=0; original.Reset(); enhanced.Reset(); road.Reset(); Clipped=false; }
        static double Filter(double previous,double input,double hz,double dt) { return previous+(input-previous)*(1-Math.Exp(-2*Math.PI*hz*dt)); }
        public void Push(Sample s,double now,FeelSettings settings) {
            road.Push(s,now,settings);
            if(settings.Enhanced && !settings.Original) { enhanced.Push(s,now); Suspension=enhanced.Suspension; Acceleration=enhanced.Acceleration; return; }
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
            double baseForce=BaseForce(now,s);
            return RoadTexture.Mix(baseForce,road.Force(now,s),s.Strength);
        }
        double BaseForce(double now,FeelSettings s) {
            if(s.Enhanced && !s.Original) { double force=enhanced.Force(now,s); Clipped=enhanced.Limited; return force; }
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
    public sealed class RecordedSample { public double At; public Sample Sample; public string Json; }
    public static class Recording {
        // Arrival time is relative to recording start; game timestamps are preserved inside JSON.
        public static List<RecordedSample> Load(string path) {
            if(new FileInfo(path).Length>64*1024*1024) throw new FormatException("Recording exceeds 64 MB. Use a shorter drive.");
            var result=new List<RecordedSample>(); double previous=-1;
            foreach(string line in File.ReadLines(path)) {
                int tab=line.IndexOf('\t'); double at;
                if(tab<0 || !double.TryParse(line.Substring(0,tab),NumberStyles.Float,CultureInfo.InvariantCulture,out at) || double.IsNaN(at)||double.IsInfinity(at)||at<0||at<previous) throw new FormatException("Invalid recording timestamp");
                string json=line.Substring(tab+1); result.Add(new RecordedSample { At=at,Sample=Sample.Parse(json),Json=json }); previous=at;
            }
            if(result.Count==0) throw new FormatException("Recording is empty"); return result;
        }
        public static string Line(double at,string json) { return at.ToString("R",CultureInfo.InvariantCulture)+"\t"+json; }
    }
}
