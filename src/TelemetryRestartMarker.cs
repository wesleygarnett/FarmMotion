// GPL-2.0-only. Out-of-band receiver generation; never recycle a healthy pipe on a timer.
using System;
using System.IO;
using System.Text;
namespace FarmMotion {
    static class TelemetryRestartMarker {
        internal static string DefaultPath {
            get {
                string profile=Environment.GetEnvironmentVariable("FARMMOTION_FS25_PROFILE");
                if(string.IsNullOrWhiteSpace(profile)) profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"My Games","FarmingSimulator2025");
                return Path.Combine(profile,"farmMotionReceiver.txt");
            }
        }
        internal static void Publish(string path) {
            // Do not invent a game profile when the path is wrong or FS25 is not installed.
            if(!Directory.Exists(Path.GetDirectoryName(path))) throw new DirectoryNotFoundException("FS25 profile folder not found");
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temporary,Guid.NewGuid().ToString("N")+"\n",new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
