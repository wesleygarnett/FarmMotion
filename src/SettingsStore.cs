// GPL-2.0-only. Bounded reads and atomic replacement of the user's tuning.
using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace FarmMotion {
    static class SettingsStore {
        public static FeelSettings Load(string path) {
            if(!File.Exists(path)) return new FeelSettings();
            if(new FileInfo(path).Length>65536) throw new FormatException("Settings file is too large.");
            var result=new JavaScriptSerializer { MaxJsonLength=65536,RecursionLimit=8 }.Deserialize<FeelSettings>(File.ReadAllText(path)) ?? new FeelSettings();
            result.Validate(); return result;
        }
        public static void Save(string path,FeelSettings settings) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(settings),new UTF8Encoding(false));
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
