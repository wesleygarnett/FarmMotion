// GPL-2.0-only. Bounded asynchronous recording keeps disk I/O off the force lock.
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
namespace FarmMotion {
    sealed class RecordingWriter : IDisposable {
        readonly BlockingCollection<string> pending=new BlockingCollection<string>(2048);
        readonly Thread worker; volatile string failure; bool disposed;
        public string Error { get { return failure; } }
        public RecordingWriter(string path) {
            var writer=new StreamWriter(path,false);
            worker=new Thread(delegate() {
                try { using(writer) foreach(string line in pending.GetConsumingEnumerable()) writer.WriteLine(line); }
                catch(Exception e) { failure=e.Message; }
            }) { IsBackground=true,Name="FarmMotion recording" }; worker.Start();
        }
        public void WriteLine(string line) {
            if(failure!=null) throw new IOException(failure);
            if(!pending.TryAdd(line)) throw new IOException("Recording storage cannot keep up; recording stopped.");
        }
        public void Dispose() {
            if(disposed) return; disposed=true;
            pending.CompleteAdding();
            if(!worker.Join(2000)) { failure="Recording is still finishing on slow storage."; return; }
            pending.Dispose();
        }
    }
}
