// GPL-2.0-only. Hardware-free checks, including the real Windows pipe transport.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
namespace FarmMotion {
    static class Tests {
        static int count;
        static void Assert(bool ok,string name) { if(!ok) throw new Exception("FAIL: "+name); count++; Console.WriteLine("PASS: "+name); }
        static Sample S(int seq,double y) { return Sample.Parse("{\"v\":1,\"active\":true,\"session\":\"a\",\"vehicle\":\"1\",\"seq\":"+seq+",\"time\":"+(seq*0.02).ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"wheels\":[{\"i\":1,\"y\":"+y.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}]}"); }
        public static int Run() {
            ReleaseTests.Run(Assert);
            var m=new Motion(); m.Push(S(1,0),0); m.Push(S(2,0),0.02); Assert(m.Level(.02)==0,"Stationary vehicle is quiet");
            m.Push(S(3,.004),.04); double small=m.Level(.04); Assert(small>0,"Suspension bump creates vibration");
            m.Reset(); m.Push(S(1,0),0); m.Push(S(2,.008),.02); Assert(m.Level(.02)>small,"Larger movement produces stronger vibration");
            Assert(m.Level(.18)==0,"Stale telemetry stops vibration");
            for(int i=3;i<100;i++) m.Push(S(i,.008),i*.02); Assert(m.Level(1.98)<.001,"Settled suspension returns to silence");
            m.Reset(); var body=S(2,0); body.Vy=.12; var start=S(1,0); start.Vy=0; m.Push(start,0); m.Push(body,.02); Assert(m.Level(.02)>0,"Chassis acceleration works without suspension movement");
            var swap=S(3,1); swap.Vehicle="2"; m.Push(swap,.04); Assert(m.Level(.04)==0,"Vehicle change does not cause a kick");
            m.Reset(); m.Push(S(1,0),0); m.Push(S(2,10),.02); Assert(m.Level(.02)==0,"Discontinuous suspension values are suppressed");
            m.Reset(); m.Push(S(1,0),0); m.Push(S(2,.008),.02); m.Push(S(2,1),.03); Assert(m.Level(.03)>0,"Duplicate packet is ignored");
            m.Push(Sample.Parse("{\"v\":1,\"active\":false}"),.04); Assert(m.Level(.04)==0,"Pause or vehicle exit stops output");
            m.Reset(); m.Push(S(1,0),0); var wheel=S(2,.01); wheel.Wheels.Clear(); wheel.Wheels[2]=100; m.Push(wheel,.02); Assert(m.Level(.02)==0,"New wheel cannot create a derivative spike");
            bool rejected=false; try { Sample.Parse("{\"v\":1,\"active\":true}"); } catch { rejected=true; } Assert(rejected,"Incomplete active packet rejected");
            m.Reset(); m.Push(S(1,0),0); m.Push(S(2,.008),.02); bool cap=true; for(int i=20;i<150;i++) cap &= Math.Abs(m.Force(i/1000.0,1))<=.1; Assert(cap,"Output remains capped");
            m.Reset(); m.Sensitivity=.5; m.Push(S(1,0),0); m.Push(S(2,.004),.02); double softer=m.Level(.02);
            m.Reset(); m.Sensitivity=2; m.Push(S(1,0),0); m.Push(S(2,.004),.02); Assert(m.Level(.02)>softer,"Sensitivity adjusts response to the same movement");
            string pipeName="FarmMotionTest-"+Guid.NewGuid().ToString("N");
            var timer=Stopwatch.StartNew(); using(var receiver=new Receiver(timer,pipeName)) {
                using(var client=new NamedPipeClientStream(".",pipeName,PipeDirection.Out)) {
                    client.Connect(3000); using(var writer=new StreamWriter(client)) { writer.WriteLine("{\"v\":1,\"active\":false}"); writer.Flush(); for(int i=0;i<100 && receiver.Packets==0;i++) Thread.Sleep(10); Assert(receiver.Packets==1 && receiver.Invalid==0,"JSON traverses actual named pipe"); }
                }
                Thread.Sleep(100);
                using(var client=new NamedPipeClientStream(".",pipeName,PipeDirection.Out)) {
                    client.Connect(3000);
                    using(var writer=new StreamWriter(client)) {
                        writer.WriteLine("bad json"); writer.Flush();
                        for(int i=0;i<100 && receiver.Invalid==0;i++) Thread.Sleep(10);
                        Assert(receiver.Invalid==1 && receiver.ConnectionErrors==0 && receiver.LastError.StartsWith("Packet:"),"Malformed telemetry is identified separately from transport errors");
                        for(int i=0;i<100;i++) { writer.WriteLine("{\"v\":1,\"active\":false}"); writer.Flush(); Thread.Sleep(5); }
                        for(int i=0;i<100 && receiver.Packets<101;i++) Thread.Sleep(10);
                        Assert(receiver.Packets==101 && receiver.ConnectionErrors==0,"Reconnection and sustained stream do not create receiver errors");
                    }
                }
            }
            var config=new FeelSettings(); config.Texture=0; config.Body=0; config.Sensitivity=1;
            var shaped=new Feel(); double positive=0,negative=0;
            for(int i=1;i<100;i++) { double y=.025*Math.Sin(i*.02*2*Math.PI*2); shaped.Push(S(i,y),i*.02,config); double f=shaped.Force(i*.02,config); positive=Math.Max(positive,f); negative=Math.Min(negative,f); }
            Assert(positive>.001 && negative<-.001,"Motion-shaped bumps preserve compression and rebound direction");
            Assert(shaped.Force(3,config)==0,"New processing stops on stale telemetry");
            shaped.Reset(); shaped.Push(S(1,0),0,config); shaped.Push(S(2,10),.02,config); Assert(shaped.Force(.02,config)==0,"New processing rejects discontinuities");
            config.Original=true; var baseline=new Motion { Sensitivity=config.Sensitivity }; shaped.Reset();
            for(int i=1;i<10;i++) { var s=S(i,.002*i); shaped.Push(s,i*.02,config); baseline.Push(s,i*.02); }
            Assert(Math.Abs(shaped.Force(.18,config)-baseline.Force(.18,config.Strength))<1e-9,"Original comparison preserves original processing");
            string recording=Path.GetTempFileName();
            try {
                File.WriteAllLines(recording,new[]{Recording.Line(0,"{\"v\":1,\"active\":false}"),Recording.Line(.025,"{\"v\":1,\"active\":false}")});
                var frames=Recording.Load(recording); Assert(frames.Count==2 && frames[1].At==.025,"Recording replay preserves arrival timing");
                File.WriteAllLines(recording,new[]{Recording.Line(.1,"{\"v\":1,\"active\":false}"),Recording.Line(0,"{\"v\":1,\"active\":false}")});
                bool bad=false; try { Recording.Load(recording); } catch(FormatException) { bad=true; } Assert(bad,"Replay rejects backward timestamps");
            } finally { File.Delete(recording); }
            var detailConfig=new FeelSettings { Texture=1, Bumps=0, Body=0, Sensitivity=.25 };
            var detailed=new Feel();
            for(int i=1;i<=20;i++) detailed.Push(S(i,.002*Math.Sin(i*1.7)),i*.02,detailConfig);
            double gentle=0,strong=0; bool limited=true;
            for(int i=401;i<490;i++) {
                double t=i/1000.0; detailConfig.Texture=.5; gentle+=Math.Abs(detailed.Force(t,detailConfig));
                detailConfig.Texture=4; double force=detailed.Force(t,detailConfig); strong+=Math.Abs(force); limited &= Math.Abs(force)<=detailConfig.Strength;
            }
            Assert(strong>gentle*2,"Texture gain strengthens fine vibration without changing bump/body settings");
            Assert(limited,"Boosted texture respects the overall strength cap");
            detailConfig.Texture=1; detailConfig.TextureFrequency=12; double low=detailed.Force(.411,detailConfig);
            detailConfig.TextureFrequency=40; double high=detailed.Force(.411,detailConfig);
            Assert(Math.Abs(low-high)>1e-6,"Texture frequency changes the vibration waveform");
            Assert(detailed.Force(1,detailConfig)==0,"Texture boost cannot continue without fresh motion data");
            var frozen=new FeelV1Reference(); var current=new Feel(); var comparison=new FeelSettings(); bool exact=true;
            for(int i=1;i<=1000;i++) {
                var sample=S(i,.015*Math.Sin(i*.19)); sample.Wheels[2]=.009*Math.Sin(i*.23); sample.Vy=.1*Math.Sin(i*.1);
                if(i%157==0) sample.Active=false;
                comparison.Texture=(i%40)/10.0; comparison.Sensitivity=.25+(i%8)*.25;
                current.Push(sample,i*.02,comparison); frozen.Push(sample,i*.02,comparison);
                for(int j=0;j<4;j++) exact &= current.Force(i*.02+j*.004,comparison)==frozen.Force(i*.02+j*.004,comparison);
            }
            Assert(exact,"V1 exactly matches frozen pre-update output across 4000 force samples");
            var v2=new Feel(); var v2Settings=new FeelSettings { Enhanced=true,Texture=0,Body=0,Sensitivity=1 };
            double opposingPeak=0;
            for(int i=1;i<100;i++) { var sample=S(i,.02*Math.Sin(i*.22)); sample.Wheels[2]=-sample.Wheels[1]; v2.Push(sample,i*.02,v2Settings); opposingPeak=Math.Max(opposingPeak,Math.Abs(v2.Force(i*.02,v2Settings))); }
            Assert(opposingPeak>.001,"V2 preserves bumps from opposite-phase wheels");
            for(int i=100;i<300;i++) { var sample=S(i,0); sample.Wheels[2]=0; v2.Push(sample,i*.02,v2Settings); }
            Assert(Math.Abs(v2.Force(5.98,v2Settings))<.0001,"V2 settles to quiet after motion ends");
            v2Settings.Texture=4; v2Settings.Body=2; v2Settings.Bumps=2; v2Settings.Sensitivity=3; bool v2Cap=true;
            for(int i=300;i<500;i++) { var sample=S(i,.025*Math.Sin(i*.6)); sample.Vy=.3*Math.Sin(i*.4); v2.Push(sample,i*.02,v2Settings); v2Cap &= Math.Abs(v2.Force(i*.02+.003,v2Settings))<=v2Settings.Strength; }
            Assert(v2Cap,"V2 mixing respects maximum force under combined high gains");
            Assert(v2.Force(11,v2Settings)==0,"V2 stale-data timeout stops all layers");
            v2.Push(new Sample { Active=false },11,v2Settings); Assert(v2.Force(11,v2Settings)==0,"V2 inactive telemetry immediately stops output");
            var first=new Feel(); var second=new Feel(); bool replaySame=true;
            for(int i=1;i<80;i++) { var sample=S(i,.004*Math.Sin(i*.7)); first.Push(sample,i*.02,v2Settings); second.Push(sample,10+i*.02,v2Settings); replaySame &= Math.Abs(first.Force(i*.02+.005,v2Settings)-second.Force(10+i*.02+.005,v2Settings))<1e-10; }
            Assert(replaySame,"V2 recorded motion gives repeatable texture across replay start times");
            var oldSettings=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<FeelSettings>("{\"Strength\":0.07,\"Sensitivity\":0.25,\"Original\":false}"); oldSettings.Validate();
            Assert(!oldSettings.Enhanced && !oldSettings.Original,"Old settings retain V1 rather than switching to V2");
            current.Reset(); comparison.Enhanced=true; current.Push(S(1,0),0,comparison); current.Push(S(2,.01),.02,comparison);
            current.Reset(); frozen.Reset(); comparison.Enhanced=false;
            current.Push(S(1,0),0,comparison); frozen.Push(S(1,0),0,comparison); current.Push(S(2,.005),.02,comparison); frozen.Push(S(2,.005),.02,comparison);
            Assert(current.Force(.025,comparison)==frozen.Force(.025,comparison),"Returning to V1 after reset restores previous force behavior");
            var roadSettings=new FeelSettings { RoadTexture=.1,Strength=.07,TreadCount=40 };
            var buzz=new RoadTexture(); double roadPeak=0;
            for(int i=1;i<=30;i++) { var packet=RoadSample(i,"road","mud",true,4,5); buzz.Push(packet,i*.02,roadSettings); roadPeak=Math.Max(roadPeak,Math.Abs(buzz.Force(i*.02+.003,roadSettings))); }
            Assert(roadPeak>.0001,"Road tread buzz works with perfectly still suspension");
            var parsed=RoadSample(1,"road","mud",true,4,5);
            Assert(parsed.HasSurfaceData && parsed.Tyres[1].Contact && parsed.Tyres[1].Omega==4 && parsed.Speed==5,"Surface telemetry parses units and contact state");
            double maxBuzz=0;
            for(int i=601;i<730;i++) maxBuzz=Math.Max(maxBuzz,Math.Abs(buzz.Force(i/1000.0,roadSettings)));
            Assert(maxBuzz<=.00175,"Road buzz ceiling is one quarter of the previous strength");
            Assert(buzz.Force(.76,roadSettings)==0,"Stale surface telemetry stops road buzz");
            foreach(string surface in new[]{"field","soft","hard","unknown"}) {
                buzz.Reset(); for(int i=1;i<30;i++) buzz.Push(RoadSample(i,surface,"mud",true,4,5),i*.02,roadSettings);
                Assert(buzz.Force(.583,roadSettings)==0,"No synthesized road buzz on "+surface);
            }
            buzz.Reset(); for(int i=1;i<30;i++) buzz.Push(RoadSample(i,"road","street",true,4,5),i*.02,roadSettings);
            Assert(buzz.Force(.583,roadSettings)==0,"Street tyres do not receive agricultural tread buzz");
            buzz.Reset(); for(int i=1;i<30;i++) buzz.Push(RoadSample(i,"road","mud",true,4,5),i*.02,roadSettings);
            for(int i=30;i<50;i++) buzz.Push(RoadSample(i,"road","mud",false,4,5),i*.02,roadSettings);
            Assert(Math.Abs(buzz.Force(.983,roadSettings))<1e-7,"Loss of ground contact fades road buzz");
            buzz.Reset(); for(int i=1;i<30;i++) buzz.Push(RoadSample(i,"road","mud",true,4,0),i*.02,roadSettings);
            Assert(buzz.Force(.583,roadSettings)==0,"Stationary wheelspin does not produce rolling-road buzz");
            buzz.Push(S(31,0),.62,roadSettings); Assert(buzz.Force(.623,roadSettings)==0,"Older mod packets cannot create road buzz");
            Assert(RoadTexture.Mix(.07,.005,.07)==.07 && RoadTexture.Mix(-.07,-.005,.07)==-.07,"Large bumps keep priority and the original strength ceiling");
            roadSettings.RoadTexture=0; buzz.Push(RoadSample(32,"road","mud",true,4,5),.64,roadSettings); Assert(buzz.Force(.643,roadSettings)==0,"Road slider zero restores previous output");
            Assert(oldSettings.RoadFrequency==75,"Older settings acquire the independent 75 Hz road pitch");
            roadSettings.RoadTexture=.1;
            foreach(double frequency in new[]{50.0,75.0,90.0}) {
                roadSettings.RoadFrequency=frequency; buzz.Reset();
                int crossings=0; double last=0;
                for(int i=0;i<1500;i++) {
                    double at=i*.002;
                    if(i%10==0) buzz.Push(RoadSample(i/10+1,"road","mud",true,4,5),at,roadSettings);
                    double f=buzz.Force(at,roadSettings);
                    if(i>=500 && last<=0 && f>0) crossings++;
                    last=f;
                }
                Assert(Math.Abs(crossings-frequency*2)<=1,"Road pitch remains "+frequency+" Hz across telemetry packets");
            }
            roadSettings.RoadFrequency=75; buzz.Reset(); bool continuous=true;
            double firstArrival=.02+.002*Math.Sin(1);
            for(int i=1;i<=300;i++) {
                double arrival=i*.02+.002*Math.Sin(i);
                buzz.Push(RoadSample(i,"road","mud",true,4,5),arrival,roadSettings);
                if(i>200) {
                    double expected=.00175*Math.Sin(2*Math.PI*75*(arrival+.003-firstArrival));
                    continuous &= Math.Abs(buzz.Force(arrival+.003,roadSettings)-expected)<1e-10;
                }
            }
            Assert(continuous,"Road carrier stays phase-continuous with jittery packet arrivals");
            var oneTyre=new RoadTexture(); var twoTyres=new RoadTexture(); bool coherent=true;
            for(int i=1;i<=200;i++) {
                var packet=RoadSample(i,"road","mud",true,4,5);
                oneTyre.Push(packet,i*.02,roadSettings);
                packet.Tyres[2]=RoadSample(i,"road","mud",true,4.1,5).Tyres[1];
                twoTyres.Push(packet,i*.02,roadSettings);
                coherent &= Math.Abs(oneTyre.Force(i*.02+.003,roadSettings)-twoTyres.Force(i*.02+.003,roadSettings))<1e-12;
            }
            Assert(coherent,"Slightly different tyre speeds cannot create slow beating or cancellation");
            buzz.Push(new Sample { Active=false },7,roadSettings);
            Assert(buzz.Force(7,roadSettings)==0,"Inactive telemetry immediately stops the high-frequency road layer");
            roadSettings.RoadFrequency=double.NaN; roadSettings.Validate();
            Assert(roadSettings.RoadFrequency==75,"Invalid road frequency restores the default");
            roadSettings.RoadFrequency=500; roadSettings.Validate();
            Assert(roadSettings.RoadFrequency==90,"Road pitch remains within the supported software range");
            Console.WriteLine(count+" checks passed; no wheel output was enabled."); return 0;
        }
        static Sample RoadSample(int seq,string surface,string tire,bool contact,double omega,double speed) {
            var culture=System.Globalization.CultureInfo.InvariantCulture;
            return Sample.Parse("{\"v\":1,\"surfaceVersion\":1,\"active\":true,\"session\":\"road\",\"vehicle\":\"1\",\"seq\":"+seq+",\"time\":"+(seq*.02).ToString(culture)+",\"speed\":"+speed.ToString(culture)+",\"wheels\":[{\"i\":1,\"y\":0,\"surface\":\""+surface+"\",\"tire\":\""+tire+"\",\"contact\":"+(contact ? "true":"false")+",\"omega\":"+omega.ToString(culture)+"}]}");
        }
    }
}
