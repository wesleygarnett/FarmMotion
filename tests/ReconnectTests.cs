// GPL-2.0-only. Real Windows pipe lifecycle, isolated from production telemetry.
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
        public static void Run(Action<bool,string> assert) {
            string dir=Path.Combine(Path.GetTempPath(),"FarmMotionReconnect-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            string marker=Path.Combine(dir,"farmMotionReceiver.txt"), name="FarmMotionTest-"+Guid.NewGuid().ToString("N");
            Receiver receiver=null; NamedPipeClientStream client=null; var clock=Stopwatch.StartNew();
            try {
                receiver=new Receiver(clock,name,marker);
                assert(Wait(()=>File.Exists(marker)),"Receiver publishes restart marker before accepting telemetry");
                string first=File.ReadAllText(marker); assert(first.Length==33&&first[32]=='\n',"Restart marker uses complete bounded token format");
                client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==1),"Restart test receives initial telemetry");
                client.Dispose(); client=null;
                client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==2)&&File.ReadAllText(marker)==first,"Normal client reconnect leaves instance token unchanged");
                receiver.Dispose(); receiver=null;
                bool broken=false;
                for(int i=0;i<100&&!broken;i++) { try { Send(client); } catch(IOException) { broken=true; } Thread.Sleep(10); }
                assert(broken,"Old producer handle breaks after receiver shutdown");
                receiver=new Receiver(clock,name,marker);
                assert(Wait(()=>File.ReadAllText(marker)!=first),"Replacement receiver publishes a new instance token");
                assert(receiver.Packets==0,"Replacement receiver cannot receive from stale producer handle");
                client.Dispose(); client=new NamedPipeClientStream(".",name,PipeDirection.Out); client.Connect(3000); Send(client);
                assert(Wait(()=>receiver.Packets==1),"Reopening producer after app restart restores telemetry");
                assert(Directory.GetFiles(dir,"*.tmp").Length==0,"Marker publication leaves no temporary files");
            } finally { if(client!=null) client.Dispose(); if(receiver!=null) receiver.Dispose(); Directory.Delete(dir,true); }
        }
    }
}
