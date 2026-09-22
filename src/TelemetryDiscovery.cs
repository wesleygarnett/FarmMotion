// GPL-2.0-only. Restart discovery without files, profile paths or elevated permissions.
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
namespace FarmMotion {
    sealed class TelemetryDiscovery : IDisposable {
        readonly object gate=new object();
        readonly string name;
        readonly byte[] token=Encoding.ASCII.GetBytes(Guid.NewGuid().ToString("N")+"\n");
        NamedPipeServerStream pipe;
        volatile bool done;
        public volatile string Error="";
        public TelemetryDiscovery(string name) { this.name=name; new Thread(Listen) { IsBackground=true,Name="FarmMotion restart discovery" }.Start(); }
        void Listen() {
            while(!done) {
                bool connected=false;
                try {
                    using(var outgoing=new NamedPipeServerStream(name,PipeDirection.Out,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,0,4096,Receiver.LocalPipeSecurity())) {
                        lock(gate) { if(done) return; pipe=outgoing; }
                        Error="";
                        outgoing.WaitForConnection(); connected=true;
                        // A reader that never consumes the token must not monopolize discovery.
                        using(var deadline=new Timer(delegate { try { outgoing.Disconnect(); } catch(ObjectDisposedException) { } catch(IOException) { } catch(InvalidOperationException) { } },null,1000,Timeout.Infinite)) {
                            outgoing.Write(token,0,token.Length);
                            outgoing.WaitForPipeDrain();
                        }
                    }
                } catch(IOException e) { if(!done) { if(!connected) Error="Automatic reconnect unavailable: "+e.Message; Thread.Sleep(250); } }
                  catch(ObjectDisposedException) { if(!done) Thread.Sleep(50); }
                  catch(Exception e) { if(!done) { Error="Automatic reconnect unavailable: "+e.Message; Thread.Sleep(1000); } }
                finally { lock(gate) pipe=null; }
            }
        }
        public void Dispose() {
            lock(gate) {
                done=true;
                if(pipe!=null) {
                    try { if(pipe.IsConnected) pipe.Disconnect(); } catch(IOException) { } catch(InvalidOperationException) { }
                    pipe.Dispose();
                }
            }
        }
    }
}
