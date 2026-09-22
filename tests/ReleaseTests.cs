// GPL-2.0-only. Regression checks for release hardening.
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Text;
using System.Threading;
namespace FarmMotion {
    static class ReleaseTests {
        public static void Run(Action<bool,string> assert) {
            var rules=Receiver.LocalPipeSecurity().GetAccessRules(true,true,typeof(SecurityIdentifier));
            bool networkDenied=false,currentUserAllowed=false; int allows=0;
            using(var user=WindowsIdentity.GetCurrent()) foreach(PipeAccessRule rule in rules) {
                if(rule.AccessControlType==AccessControlType.Deny && rule.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.NetworkSid,null))) networkDenied=true;
                if(rule.AccessControlType==AccessControlType.Allow) { allows++; currentUserAllowed=rule.IdentityReference.Equals(user.User); }
            }
            assert(networkDenied && currentUserAllowed && allows==1,"Telemetry ACL allows only current user and denies network clients");
            bool large=false; try { Sample.Parse(new string('x',16385)); } catch(FormatException) { large=true; }
            assert(large,"Oversized telemetry rejected before JSON deserialization");
            bool identity=false; try { Sample.Parse("{\"v\":1,\"active\":true,\"session\":\"a\",\"vehicle\":\""+new string('x',129)+"\",\"time\":0,\"seq\":0}"); } catch(FormatException) { identity=true; }
            assert(identity,"Oversized telemetry identity rejected");
            string pipeName="FarmMotionConsumerTest"+Guid.NewGuid().ToString("N");
            using(var receiver=new Receiver(Stopwatch.StartNew(),pipeName,pipeName+"Session")) {
                receiver.SampleReceived+=delegate { throw new InvalidOperationException("test consumer failure"); };
                using(var client=new NamedPipeClientStream(".",pipeName,PipeDirection.Out)) {
                    client.Connect(3000); byte[] packets=Encoding.UTF8.GetBytes("{\"v\":1,\"active\":false}\n{\"v\":1,\"active\":false}\n"); client.Write(packets,0,packets.Length); client.Flush();
                    for(int i=0;i<300 && receiver.Packets<2;i++) Thread.Sleep(10);
                }
                assert(receiver.Packets==2 && receiver.Invalid==0 && receiver.ConnectionErrors==0,"Telemetry consumer failures do not reject valid packets or break the connection");
            }
            string folder=Path.Combine(Path.GetTempPath(),"FarmMotionReleaseTest-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            try {
                string path=Path.Combine(folder,"settings.json");
                SettingsStore.Save(path,new FeelSettings { Strength=.05 }); SettingsStore.Save(path,new FeelSettings { Strength=.08,Enhanced=true });
                var saved=SettingsStore.Load(path);
                assert(saved.Strength==.08 && saved.Enhanced,"Atomic settings replacement preserves latest tuning");
                assert(Directory.GetFiles(folder,"*.tmp").Length==0,"Atomic settings leave no staging file behind");
                File.WriteAllText(path,new string('x',65537)); bool bounded=false;
                try { SettingsStore.Load(path); } catch(FormatException) { bounded=true; }
                assert(bounded,"Oversized settings rejected");
                string recording=Path.Combine(folder,"drive.fmr");
                using(var writer=new RecordingWriter(recording)) for(int i=0;i<100;i++) writer.WriteLine(Recording.Line(i*.02,"{\"v\":1,\"active\":false}"));
                var frames=Recording.Load(recording);
                assert(frames.Count==100 && Math.Abs(frames[99].At-1.98)<1e-9,"Async recording flushes all frames in order on close");
            } finally { Directory.Delete(folder,true); }
        }
    }
}
