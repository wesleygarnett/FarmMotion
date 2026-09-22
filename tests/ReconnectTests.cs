// GPL-2.0-only. Real Windows restart discovery with no filesystem marker.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
namespace FarmMotion {
    static class ReconnectTests {
        static bool Wait(Func<bool> ready) { for(int i=0;i<300;i++) { if(ready()) return true; Thread.Sleep(10); } return false; }
        static void Send(NamedPipeClientStream client) { byte[] b=Encoding.UTF8.GetBytes("{\"v\":1,\"active\":false}\n"); client.Write(b,0,b.Length); client.Flush(); }
        static string ReadToken(string name) {
            using(var client=new NamedPipeClientStream(".",name,PipeDirection.In,PipeOptions.Asynchronous)) {
                client.Connect(3000); var bytes=new byte[33]; int used=0;
                while(used<bytes.Length) {
                    var read=client.BeginRead(bytes,used,bytes.Length-used,null,null);
                    int count;
                    using(var completed=read.AsyncWaitHandle) { if(!completed.WaitOne(3000)) throw new Exception("Discovery read timed out"); count=client.EndRead(read); }
                    if(count==0) break; used+=count;
                }
                return Encoding.ASCII.GetString(bytes,0,used);
            }
        }
        public static void Run(Action<bool,string> assert) {
            string name="FarmMotionTest-"+Guid.NewGuid().ToString("N"), discovery=name+"Session";
            Receiver receiver=null; NamedPipeClientStream client=null; var clock=Stopwatch.StartNew();
            try {
                receiver=new Receiver(clock,name);
                string first=ReadToken(discovery);
                assert(first.Length==33&&first[32]=='\n',"Restart discovery sends bounded token without profile files");
                assert(receiver.RestartMarkerError=="","Discovery starts without Documents permissions or a game profile");
                client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==1),"Restart test receives initial telemetry");
                for(int i=0;i<5;i++) assert(ReadToken(discovery)==first,"Repeated discovery preserves the active instance token");
                client.Dispose(); client=null;
                client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==2)&&ReadToken(discovery)==first,"Normal telemetry reconnect leaves discovery token unchanged");
                using(var stalled=new NamedPipeClientStream(".",discovery,PipeDirection.In)) {
                    stalled.Connect(3000); Thread.Sleep(1200);
                    assert(ReadToken(discovery)==first,"A nonreading client cannot block future discovery");
                }
                receiver.Dispose(); receiver=null;
                bool broken=false;
                for(int i=0;i<100&&!broken;i++) { try { Send(client); } catch(IOException) { broken=true; } Thread.Sleep(10); }
                assert(broken,"Old producer handle breaks after receiver shutdown");
                receiver=new Receiver(clock,name);
                assert(ReadToken(discovery)!=first,"Replacement receiver exposes a new instance token");
                assert(receiver.Packets==0,"Replacement receiver cannot receive from stale producer handle");
                client.Dispose(); client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==1),"Reopening producer after app restart restores telemetry");
            } finally { if(client!=null) client.Dispose(); if(receiver!=null) receiver.Dispose(); }
        }
    }
}
