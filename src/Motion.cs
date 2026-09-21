// GPL-2.0-only. See LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace FarmMotion {
    public sealed class Sample {
        public sealed class Tyre { public bool Contact; public string Surface,Tire; public double? Omega; }
        public Dictionary<int,Tyre> Tyres=new Dictionary<int,Tyre>();
        public double? Speed;
        public bool HasSurfaceData;
        public bool Active;
        public string Session, Vehicle;
        public double Time, Seq;
        public double? Vy, Pitch, Roll;
        public Dictionary<int,double> Wheels = new Dictionary<int,double>();
        static double? Number(IDictionary<string,object> d, string key) {
            object v; double n;
            if (!d.TryGetValue(key,out v) || v == null || v is bool || v is string) return null;
            try { n = Convert.ToDouble(v); } catch { return null; }
            return double.IsNaN(n) || double.IsInfinity(n) ? (double?)null : n;
        }
        public static Sample Parse(string line) {
            if (line == null || line.Length > 16384) throw new FormatException("Packet too large");
            var d = new JavaScriptSerializer { MaxJsonLength = 16384, RecursionLimit = 8 }.Deserialize<Dictionary<string,object>>(line);
            if (d == null || Number(d,"v") != 1) throw new FormatException("Unknown protocol");
            var s = new Sample(); object value;
            s.HasSurfaceData=Number(d,"surfaceVersion")==1;
            s.Active = d.TryGetValue("active",out value) && value is bool && (bool)value;
            if (!s.Active) return s;
            if (!d.TryGetValue("session",out value) || !(value is string)) throw new FormatException("Missing session");
            s.Session = (string)value;
            if (!d.TryGetValue("vehicle",out value) || !(value is string)) throw new FormatException("Missing vehicle");
            s.Vehicle = (string)value;
            if(s.Session.Length>128 || s.Vehicle.Length>128) throw new FormatException("Identity is too long");
            var time=Number(d,"time"); var seq=Number(d,"seq");
            if (!time.HasValue || !seq.HasValue) throw new FormatException("Missing clock");
            s.Time=time.Value; s.Seq=seq.Value;
            s.Speed=Number(d,"speed");
            s.Vy=Number(d,"vy"); s.Pitch=Number(d,"pitchRate"); s.Roll=Number(d,"rollRate");
            if (d.TryGetValue("wheels",out value) && value is IEnumerable) {
                foreach (object item in (IEnumerable)value) {
                    var w=item as IDictionary<string,object>; if(w==null) continue;
                    var i=Number(w,"i"); var y=Number(w,"y");
                    if(i.HasValue && y.HasValue && i>=1 && i<=16 && i==Math.Floor(i.Value)) s.Wheels[(int)i.Value]=y.Value;
                    if(i.HasValue && i>=1 && i<=16 && i==Math.Floor(i.Value)) {
                        var tyre=new Tyre { Omega=Number(w,"omega") }; object field;
                        tyre.Contact=w.TryGetValue("contact",out field) && field is bool && (bool)field;
                        tyre.Surface=w.TryGetValue("surface",out field) ? field as string : null;
                        tyre.Tire=w.TryGetValue("tire",out field) ? field as string : null;
                        s.Tyres[(int)i.Value]=tyre;
                    }
                }
            }
            return s;
        }
    }
    public sealed class Motion {
        public double Sensitivity=1;
        Sample previous;
        double envelope, received = double.NegativeInfinity;
        public void Reset() { previous=null; envelope=0; received=double.NegativeInfinity; }
        public void Push(Sample s, double now) {
            if (!s.Active) { Reset(); return; }
            var p=previous;
            if(p!=null && s.Session==p.Session && s.Vehicle==p.Vehicle && s.Seq<=p.Seq) return;
            previous=s; received=now;
            double dt=p==null ? 0 : s.Time-p.Time;
            if(p==null || s.Session!=p.Session || s.Vehicle!=p.Vehicle || dt<0.001 || dt>0.15) { envelope=0; return; }
            double rate=0; int count=0;
            foreach(var w in s.Wheels) { double y; if(p.Wheels.TryGetValue(w.Key,out y)) { rate+=Math.Abs(w.Value-y)/dt; count++; } }
            rate=count==0 ? 0 : rate/count;
            double accel=s.Vy.HasValue && p.Vy.HasValue ? Math.Abs(s.Vy.Value-p.Vy.Value)/dt : 0;
            if(rate>3 || accel>50) { envelope=0; return; }
            double angular=0;
            if(s.Pitch.HasValue && p.Pitch.HasValue) angular+=Math.Abs(s.Pitch.Value-p.Pitch.Value)/dt;
            if(s.Roll.HasValue && p.Roll.HasValue) angular+=Math.Abs(s.Roll.Value-p.Roll.Value)/dt;
            if(angular>50) { envelope=0; return; }
            double target=Math.Min(1,Sensitivity*Math.Max(Math.Max(0,rate-0.025)/0.45,Math.Max(Math.Max(0,accel-0.25)/6,Math.Max(0,angular-0.2)/10)));
            envelope = target>envelope ? target : target+(envelope-target)*Math.Exp(-dt/0.07);
        }
        public double Level(double now) { double age=now-received; return age<0 || age>=0.15 ? 0 : envelope*Math.Min(1,(0.15-age)/0.05); }
        public double Force(double now,double strength) { return Level(now)*Math.Max(0,Math.Min(FeelSettings.MaximumStrength,strength))*Math.Sin(2*Math.PI*18*now); }
    }
}
