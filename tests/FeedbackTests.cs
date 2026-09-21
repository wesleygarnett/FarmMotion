// GPL-2.0-only. Channel routing and carrier-independent controller output checks.
using System;
namespace FarmMotion {
    public static class FeedbackTests {
        static int count;
        static void Check(bool condition,string message) { if(!condition) throw new Exception("Feedback: "+message); count++; }
        static Sample Frame(int i) {
            var s=new Sample { Active=true,Session="channels",Vehicle="tractor",Seq=i,Time=i*.02,Vy=.18*Math.Sin(i*.17),Speed=5,HasSurfaceData=true };
            s.Wheels[1]=.008*Math.Sin(i*.23); s.Wheels[2]=.006*Math.Sin(i*.37);
            s.Tyres[1]=new Sample.Tyre { Contact=true,Surface="road",Tire="mud",Omega=4 };
            return s;
        }
        public static int Run() {
            count=0;
            var frozen=new FrozenFeelV2(); var current=new FeelV2();
            var regressionSettings=new FeelSettings { Strength=.1,Sensitivity=3,Bumps=2,Body=2,Texture=4 };
            bool regression=true,sawLimited=false,sawStale=false;
            for(int i=1;i<=4000;i++) {
                var sample=Frame(i); double now=i*.02;
                if(i%317==0) sample.Active=false;
                if(i%113==0) sample.Wheels[1]=10;
                regressionSettings.Texture=i%80<40 ? 4:0;
                current.Push(sample,now); frozen.Push(sample,now);
                double at=now+(i%9==0 ? .16:i%7==0 ? -.01:.003);
                double expected=frozen.Force(at,regressionSettings),actual=current.Force(at,regressionSettings);
                regression &= expected==actual && frozen.Limited==current.Limited;
                sawLimited |= current.Limited; sawStale |= i%9==0 && actual==0;
            }
            Check(regression && sawLimited && sawStale,"4000 V2 outputs and limiter states exactly match frozen reference, including saturation and stale input");
            foreach(bool enhanced in new[]{false,true}) {
                var settings=new FeelSettings { Enhanced=enhanced,Strength=.08,Texture=.8,Body=.7,Bumps=1.2,RoadTexture=.1 };
                var feel=new Feel();
                Check(!feel.IsFresh(0),"empty input is not fresh");
                bool exact=true,bounded=true,independent=true,stable=true,isolated=true; double lowPeak=0,highPeak=0;
                for(int i=1;i<200;i++) {
                    double now=i*.02; feel.Push(Frame(i),now,settings);
                    exact &= feel.Force(now+.003,settings)==feel.Force(now+.003,settings,FeedbackSolo.All);
                    var rumble=feel.GetRumble(now,settings,FeedbackSolo.All);
                    var zeroStrength=settings.Copy(); zeroStrength.Strength=0;
                    var withoutWheel=feel.GetRumble(now,zeroStrength,FeedbackSolo.All);
                    independent &= rumble.Low==withoutWheel.Low && rumble.High==withoutWheel.High;
                    bounded &= rumble.Low>=0 && rumble.Low<=1 && rumble.High>=0 && rumble.High<=1;
                    lowPeak=Math.Max(lowPeak,rumble.Low); highPeak=Math.Max(highPeak,rumble.High);
                    var later=feel.GetRumble(now+.007,settings,FeedbackSolo.All);
                    stable &= later.Low==rumble.Low && later.High==rumble.High;
                    foreach(var solo in new[]{FeedbackSolo.Bumps,FeedbackSolo.Body,FeedbackSolo.Texture}) {
                        var isolatedSettings=settings.Copy(); isolatedSettings.RoadTexture=0;
                        if(solo!=FeedbackSolo.Bumps) isolatedSettings.Bumps=0;
                        if(solo!=FeedbackSolo.Body) isolatedSettings.Body=0;
                        if(solo!=FeedbackSolo.Texture) isolatedSettings.Texture=0;
                        if(!enhanced || solo==FeedbackSolo.Texture) isolated &= feel.Force(now+.003,settings,solo)==feel.Force(now+.003,isolatedSettings);
                        var channel=feel.GetRumble(now,settings,solo);
                        isolated &= solo==FeedbackSolo.Texture ? channel.Low==0 : channel.High==0;
                    }
                }
                Check(exact,"All overload preserves legacy output"); Check(independent,"rumble independent of wheel strength");
                Check(bounded && lowPeak>0 && highPeak>0,"both rumble motors receive bounded motion envelopes");
                Check(stable,"rumble does not oscillate with wheel carriers between packets"); Check(isolated,"solo excludes other channels without changing gains");
                Check(feel.IsFresh(3.98) && !feel.IsFresh(4.14),"freshness expires after telemetry timeout");
                var stale=feel.GetRumble(4.14,settings,FeedbackSolo.All);
                Check(stale.Low==0 && stale.High==0,"stale input zeros both motors");
                var zero=settings.Copy(); zero.Bumps=zero.Body=zero.Texture=zero.RoadTexture=0;
                var muted=feel.GetRumble(3.98,zero,FeedbackSolo.All); Check(muted.Low==0 && muted.High==0,"channel gain zero silences rumble");
                feel.Push(new Sample { Active=false },4,settings);
                var inactive=feel.GetRumble(4,settings,FeedbackSolo.All);
                Check(!feel.IsFresh(4) && inactive.Low==0 && inactive.High==0,"inactive input immediately gates rumble and acquisition");
            }
            var originalSettings=new FeelSettings { Original=true,RoadTexture=.1 }; var original=new Feel();
            for(int i=1;i<50;i++) original.Push(Frame(i),i*.02,originalSettings);
            Check(original.Force(.98,originalSettings,FeedbackSolo.Bumps)==0 && original.GetRumble(.98,originalSettings,FeedbackSolo.Texture).High==0,"original mode rejects unavailable motion channels");
            Check(original.GetRumble(.98,originalSettings,FeedbackSolo.Original).Low>0 && original.GetRumble(.98,originalSettings,FeedbackSolo.Road).High>0,"original and road can be auditioned independently");
            Console.WriteLine(count+" feedback channel checks passed."); return count;
        }
    }
}
