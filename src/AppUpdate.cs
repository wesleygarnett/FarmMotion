using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace FarmMotion {
    sealed class UpdateRelease { public string Version, Url, Hash, Notes, ChecksumsUrl, AssetName; }
    static class AppUpdate {
        const string Api="https://api.github.com/repos/wesleygarnett/FarmMotion/releases/latest";
        public const string ReleasesPage="https://github.com/wesleygarnett/FarmMotion/releases";
        const long MaxDownload=150*1024*1024;
        internal static readonly string[] WpfRuntimeFiles={"Wpf.Ui.dll","Wpf.Ui.Abstractions.dll","System.Memory.dll","System.Buffers.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll"};
        static readonly HashSet<string> TopFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FarmMotionUI.exe","FarmMotion.exe","FarmMotionUI.exe.config","FarmMotion.exe.config","SDL3.dll","SDL3-LICENSE.txt","FS25_FarmMotionTelemetry.zip","LICENSE","THIRD_PARTY.md","README.md","CHANGELOG.md","SECURITY.md","CONTRIBUTING.md" };
        static AppUpdate() { foreach(string name in WpfRuntimeFiles) TopFiles.Add(name); TopFiles.Add("WPF-UI-LICENSE.md"); TopFiles.Add("MICROSOFT-RUNTIME-LICENSE.txt"); }
        static byte[] Fetch(string url,string token,bool binary,long limit) {
            for(int hop=0;hop<6;hop++) {
                var uri=new Uri(url); if(uri.Scheme!="https") throw new IOException("Update requires HTTPS.");
                var req=(HttpWebRequest)WebRequest.Create(uri); req.UserAgent="FarmMotion/"+AppVersion.Display; req.Accept=binary?"application/octet-stream":"application/vnd.github+json"; req.Timeout=30000; req.ReadWriteTimeout=30000; req.AllowAutoRedirect=false;
                if(uri.Host=="api.github.com"&&!string.IsNullOrWhiteSpace(token)) req.Headers["Authorization"]="Bearer "+token.Trim();
                using(var response=(HttpWebResponse)req.GetResponse()) {
                    if((int)response.StatusCode>=300&&(int)response.StatusCode<400) { url=new Uri(uri,response.Headers["Location"]).AbsoluteUri; continue; }
                    if(response.ContentLength>limit) throw new IOException("Update download is too large.");
                    using(var stream=response.GetResponseStream()) using(var data=new MemoryStream()) { byte[] block=new byte[16384]; int read; while((read=stream.Read(block,0,block.Length))>0) { if(data.Length+read>limit) throw new IOException("Update download is too large."); data.Write(block,0,read); } return data.ToArray(); }
                }
            } throw new IOException("Too many download redirects.");
        }
        internal static Version ParseVersion(string text) { Version result; if(text==null||!System.Text.RegularExpressions.Regex.IsMatch(text,@"^v?\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$")||!Version.TryParse(text.TrimStart('v').Split('-')[0],out result)||result.Major<0) throw new FormatException("Release version is invalid."); return result; }
        internal static int CompareVersions(string left,string right) {
            int core=ParseVersion(left).CompareTo(ParseVersion(right)); if(core!=0) return core;
            string[] a=left.TrimStart('v').Split(new[]{'-'},2),b=right.TrimStart('v').Split(new[]{'-'},2); if(a.Length==1||b.Length==1) return a.Length==b.Length?0:(a.Length==1?1:-1);
            string[] aa=a[1].Split('.'),bb=b[1].Split('.'); for(int i=0;i<Math.Min(aa.Length,bb.Length);i++) { int ai,bi; bool an=int.TryParse(aa[i],out ai),bn=int.TryParse(bb[i],out bi); int c=an&&bn?ai.CompareTo(bi):(an!=bn?(an?-1:1):string.CompareOrdinal(aa[i],bb[i])); if(c!=0) return c; } return aa.Length.CompareTo(bb.Length);
        }
        public static UpdateRelease Check(string token,bool previews) {
            var serializer=new JavaScriptSerializer { MaxJsonLength=2097152 };
            Dictionary<string,object> root=null;
            if(previews) root=SelectPreview(page=>System.Text.Encoding.UTF8.GetString(Fetch(Api.Replace("/latest","?per_page=100&page="+page),token,false,2097152)));
            else root=serializer.Deserialize<Dictionary<string,object>>(System.Text.Encoding.UTF8.GetString(Fetch(Api,token,false,2097152)));
            if(root==null) throw new IOException("No published releases were found.");
            string version=(string)root["tag_name"]; if(CompareVersions(version,AppVersion.Display)<=0) return null;
            string expected="FarmMotion-v"+version.TrimStart('v')+"-win-x64.zip"; var release=new UpdateRelease { Version=version,Notes=Convert.ToString(root["body"]),AssetName=expected };
            foreach(object item in (System.Collections.IEnumerable)root["assets"]) { var asset=(Dictionary<string,object>)item; if(Convert.ToString(asset["name"])==expected) { release.Url=Convert.ToString(asset["url"]); if(asset.ContainsKey("digest")) release.Hash=Convert.ToString(asset["digest"]); } if(Convert.ToString(asset["name"])=="SHA256SUMS.txt") release.ChecksumsUrl=Convert.ToString(asset["url"]); }
            if(release.Url==null) throw new IOException("The release does not contain the Windows x64 portable package.");
            if(string.IsNullOrEmpty(release.Hash)&&release.ChecksumsUrl!=null) {
                string checks=System.Text.Encoding.UTF8.GetString(Fetch(release.ChecksumsUrl,token,true,65536));
                foreach(string line in checks.Split('\n')) { string[] parts=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries); if(parts.Length==2&&parts[1].TrimStart('*')==expected) release.Hash="sha256:"+parts[0]; }
            }
            if(string.IsNullOrEmpty(release.Hash)||!System.Text.RegularExpressions.Regex.IsMatch(release.Hash,@"^sha256:[0-9a-fA-F]{64}$")) throw new IOException("The release asset is missing its SHA-256 digest/checksum; use the release page for manual installation.");
            return release;
        }
        internal static Dictionary<string,object> SelectPreview(Func<int,string> pageSource) {
            var serializer=new JavaScriptSerializer { MaxJsonLength=2097152 }; Dictionary<string,object> result=null;
            for(int page=1;page<=10;page++) {
                var releases=serializer.Deserialize<List<Dictionary<string,object>>>(pageSource(page));
                foreach(var candidate in releases) {
                    if(Convert.ToBoolean(candidate["draft"])) continue; string tag=Convert.ToString(candidate["tag_name"]);
                    try { ParseVersion(tag); } catch(FormatException) { continue; }
                    if(result==null||CompareVersions(tag,Convert.ToString(result["tag_name"]))>0) result=candidate;
                }
                if(releases.Count<100) return result;
            }
            throw new IOException("Release history exceeds the 1,000-release check limit. Latest version could not be established; use the release page.");
        }
        internal static string Hash(byte[] data) { using(var hash=SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant(); }
        public static string Stage(UpdateRelease release,string token) {
            byte[] data=Fetch(release.Url,token,true,MaxDownload); if(!string.Equals("sha256:"+Hash(data),release.Hash,StringComparison.OrdinalIgnoreCase)) throw new IOException("Update checksum mismatch.");
            string folder=Path.Combine(Path.GetTempPath(),"FarmMotionUpdate-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"package.zip"),data); File.WriteAllText(Path.Combine(folder,"sha256.txt"),Hash(data));
            Extract(data,Path.Combine(folder,"payload"));
            ValidateVersion(Path.Combine(folder,"payload"),release.Version);
            File.WriteAllText(Path.Combine(folder,"version.txt"),release.Version);
            File.Copy(System.Windows.Forms.Application.ExecutablePath,Path.Combine(folder,"FarmMotionUpdater.exe"));
            foreach(string name in WpfRuntimeFiles) File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name),Path.Combine(folder,name));
            string config=System.Windows.Forms.Application.ExecutablePath+".config"; if(File.Exists(config)) File.Copy(config,Path.Combine(folder,"FarmMotionUpdater.exe.config"));
            return folder;
        }
        internal static void Extract(byte[] data,string target) {
            Directory.CreateDirectory(target); string prefix=Path.GetFullPath(target)+Path.DirectorySeparatorChar; long total=0; int count=0; var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using(var zip=new ZipArchive(new MemoryStream(data),ZipArchiveMode.Read)) foreach(var entry in zip.Entries) {
                string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);
                if(Path.IsPathRooted(name)||name.IndexOf(':')>=0) throw new IOException("Invalid update archive path.");
                foreach(string part in name.Split(Path.DirectorySeparatorChar)) if(part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0) throw new IOException("Invalid update archive component.");
                string path=Path.GetFullPath(Path.Combine(target,name)); if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new IOException("Update archive escapes staging.");
                if(!seen.Add(path)) throw new IOException("Duplicate update archive path.");
                if(++count>2000||(total+=entry.Length)>300*1024*1024) throw new IOException("Expanded update is too large.");
                if(string.IsNullOrEmpty(entry.Name)) continue;
                if(!Allowed(name)) throw new IOException("Unexpected update archive file: "+name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)); using(var input=entry.Open()) using(var output=File.Create(path)) input.CopyTo(output);
            }
            foreach(string name in new[]{"FarmMotionUI.exe","FarmMotion.exe","FarmMotionUI.exe.config","FarmMotion.exe.config","SDL3.dll","SDL3-LICENSE.txt"}) if(!File.Exists(Path.Combine(target,name))) throw new IOException("Update package is incomplete: "+name);
            foreach(string name in WpfRuntimeFiles) if(!File.Exists(Path.Combine(target,name))) throw new IOException("Update package is missing a WPF runtime: "+name);
        }
        static bool Allowed(string name) { if(TopFiles.Contains(name)) return true; string unix=name.Replace('\\','/'); return (unix.StartsWith("docs/",StringComparison.OrdinalIgnoreCase)||unix.StartsWith("assets/",StringComparison.OrdinalIgnoreCase)) && (unix.EndsWith(".md",StringComparison.OrdinalIgnoreCase)||unix.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||unix.EndsWith(".ico",StringComparison.OrdinalIgnoreCase)); }
        internal static void ValidateVersion(string folder,string release) { foreach(string name in new[]{"FarmMotionUI.exe","FarmMotion.exe"}) { string actual=FileVersionInfo.GetVersionInfo(Path.Combine(folder,name)).ProductVersion; if(CompareVersions(actual,release)!=0) throw new IOException("Downloaded executable version does not match the release."); } }
        static void RejectReparse(string path) { for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)) if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Update paths cannot use junctions or symbolic links."); }
        public static void BeginInstall(string folder) {
            string target=Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
            RejectReparse(target); string probe=Path.Combine(target,".farmmotion-write-"+Guid.NewGuid().ToString("N")); using(File.Create(probe)) { } File.Delete(probe);
            Process.Start(new ProcessStartInfo(Path.Combine(folder,"FarmMotionUpdater.exe"),"--apply-update "+Process.GetCurrentProcess().Id+" \""+target+"\"") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder });
        }
        public static int Apply(string[] args) {
            if(args.Length!=3) throw new ArgumentException("Invalid updater arguments."); int pid=int.Parse(args[1]);
            string stage=AppDomain.CurrentDomain.BaseDirectory; string archive=Path.Combine(stage,"package.zip"); if(new FileInfo(archive).Length>MaxDownload) throw new IOException("Staged update is too large."); byte[] data=File.ReadAllBytes(archive); if(Hash(data)!=File.ReadAllText(Path.Combine(stage,"sha256.txt"))) throw new IOException("Staged update checksum mismatch.");
            string target=Path.GetFullPath(args[2]); if(!File.Exists(Path.Combine(target,"FarmMotionUI.exe"))) throw new IOException("Update target is not a FarmMotion folder.");
            try { using(var parent=Process.GetProcessById(pid)) if(!parent.WaitForExit(30000)) throw new IOException("FarmMotion has not closed. Update cancelled."); } catch(ArgumentException) { }
            string payload=Path.Combine(stage,"verified"); Extract(data,payload); ValidateVersion(payload,File.ReadAllText(Path.Combine(stage,"version.txt")));
            InstallPayload(payload,target,delegate { using(var child=Process.Start(new ProcessStartInfo(Path.Combine(target,"FarmMotionUI.exe"),"--after-update") { WorkingDirectory=target,UseShellExecute=true })) { if(child==null||child.WaitForExit(1500)) throw new IOException("The updated application exited during startup; previous files restored."); } }); return 0;
        }
        internal static void InstallPayload(string payload,string target,Action restart) {
            RejectReparse(target);
            foreach(string source in Directory.GetFiles(payload,"*",SearchOption.AllDirectories)) RejectReparse(Path.Combine(target,source.Substring(payload.Length+1)));
            string backup=Path.Combine(target,"update-backup-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(backup);
            var installed=new List<string>(); var old=new List<string>();
            try {
                foreach(string source in Directory.GetFiles(payload,"*",SearchOption.AllDirectories)) {
                    string relative=source.Substring(payload.Length+1); string dest=Path.Combine(target,relative); Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    bool existed=File.Exists(dest); string copy=Path.Combine(backup,relative); if(existed) Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    ReplaceFrom(source,dest,existed?copy:null);
                    installed.Add(relative); if(existed) old.Add(relative);
                }
                restart();
            } catch {
                foreach(string relative in installed) { string dest=Path.Combine(target,relative); if(old.Contains(relative)) ReplaceFrom(Path.Combine(backup,relative),dest,null); else if(File.Exists(dest)) File.Delete(dest); }
                throw;
            }
        }
        // Each file is flushed beside its destination before an atomic replace/move.
        // The entire multi-file update is not atomic; backups allow manual power-loss recovery.
        static void ReplaceFrom(string source,string destination,string backup) {
            string temporary=destination+".update-"+Guid.NewGuid().ToString("N");
            try {
                using(var input=File.OpenRead(source)) using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { input.CopyTo(output); output.Flush(true); }
                if(File.Exists(destination)) File.Replace(temporary,destination,backup); else File.Move(temporary,destination);
            } finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
