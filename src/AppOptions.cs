using System;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace FarmMotion {
    sealed class AppOptions {
        public bool EnableOutputOnStartup=true, WheelEnabled=true, ControllerEnabled=true;
        public bool AllowBluetoothRumble=false;
        public bool IncludePreviewUpdates=true;
        public string WheelId="", ControllerId="";
        public double ControllerStrength=.5;
        public AppOptions Copy() { return (AppOptions)MemberwiseClone(); }
        internal static string DefaultPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmMotion","app-options.json"); } }
        public void Validate() { if(double.IsNaN(ControllerStrength)||double.IsInfinity(ControllerStrength)) ControllerStrength=.5; ControllerStrength=Math.Max(0,Math.Min(1,ControllerStrength)); WheelId=WheelId??""; ControllerId=ControllerId??""; }
        public static string LoadError="";
        public static AppOptions Load() { try { LoadError=""; return Load(DefaultPath); } catch(Exception ex) { LoadError="App preferences could not be loaded: "+ex.Message; return new AppOptions(); } }
        internal static AppOptions Load(string path) { if(!File.Exists(path)) return new AppOptions(); if(new FileInfo(path).Length>65536) throw new FormatException("App preferences are too large."); var result=new JavaScriptSerializer().Deserialize<AppOptions>(File.ReadAllText(path))??new AppOptions(); result.Validate(); return result; }
        public void Save() { Save(DefaultPath); }
        internal void Save(string path) { Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)); string tmp=path+".tmp"; try { File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(this)); if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path); } finally { if(File.Exists(tmp)) File.Delete(tmp); } }
    }
    static class AppVersion {
        public static string Display { get { return ((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(typeof(AppVersion).Assembly,typeof(AssemblyInformationalVersionAttribute))).InformationalVersion; } }
    }
    static class StartupRegistration {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool Enabled { get { using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key!=null && key.GetValue("FarmMotion")!=null; } }
        public static void Set(bool enabled) { using(var key=Registry.CurrentUser.CreateSubKey(Key)) { if(enabled) key.SetValue("FarmMotion",Command(System.Windows.Forms.Application.ExecutablePath)); else key.DeleteValue("FarmMotion",false); } }
        internal static string Command(string path) { if(path.IndexOf('"')>=0 || path.Length>220) throw new ArgumentException("Startup executable path is invalid or too long."); return "\""+Path.GetFullPath(path)+"\" --startup"; }
    }
}
