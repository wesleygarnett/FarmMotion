using System;
using System.IO;
using System.IO.Compression;
namespace FarmMotion {
    static class LifecycleTests {
        public static void Run(Action<bool,string> assert) {
            var defaults=new AppOptions(); assert(defaults.EnableOutputOnStartup&&defaults.WheelEnabled&&defaults.ControllerEnabled&&!defaults.AllowBluetoothRumble,"Default preferences enable outputs but leave Bluetooth enhanced reports opt-in");
            string temp=Path.Combine(Path.GetTempPath(),"FarmMotionLifecycle-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            try {
                string path=Path.Combine(temp,"options.json"); defaults.ControllerId="controller-id"; defaults.WheelId="wheel-id"; defaults.ControllerStrength=.37; defaults.Save(path); var loaded=AppOptions.Load(path);
                assert(loaded.ControllerId=="controller-id"&&loaded.WheelId=="wheel-id"&&loaded.ControllerStrength==.37,"App preferences preserve device selection and independent rumble strength");
                loaded.EnableOutputOnStartup=false; loaded.Save(path); assert(!AppOptions.Load(path).EnableOutputOnStartup,"Explicit startup output opt-out persists");
                File.WriteAllText(path,"{}"); assert(AppOptions.Load(path).EnableOutputOnStartup,"Missing preference fields receive new defaults");
                loaded.ControllerStrength=double.NaN; loaded.Validate(); assert(loaded.ControllerStrength==.5,"Invalid rumble gain restores bounded default");
                assert(AppUpdate.ParseVersion("v0.10.0")>AppUpdate.ParseVersion("v0.2.0"),"Update versions compare numerically");
                assert(AppUpdate.CompareVersions("v0.3.0-preview.10","v0.3.0-preview.2")>0&&AppUpdate.CompareVersions("v0.3.0","v0.3.0-preview.10")>0,"Preview versions order numeric identifiers below stable releases");
                string firstPage="["+string.Join(",",new System.Collections.Generic.List<string>(System.Linq.Enumerable.Repeat("{\"draft\":false,\"tag_name\":\"v0.3.0-preview.2\"}",100)).ToArray())+"]";
                int requests=0;
                var newest=AppUpdate.SelectPreview(page=> { requests++; return page==1?firstPage:"[{\"draft\":true,\"tag_name\":\"v99.0.0\"},{\"draft\":false,\"tag_name\":\"invalid\"},{\"draft\":false,\"tag_name\":\"v0.3.0\"}]"; });
                assert(requests==2&&Convert.ToString(newest["tag_name"])=="v0.3.0","Preview lookup follows pages, ignores drafts and invalid tags, and ranks stable above previews");
                requests=0; bool historyBounded=false; try { AppUpdate.SelectPreview(page=> { requests++; return firstPage; }); } catch(IOException) { historyBounded=true; } assert(historyBounded&&requests==10,"Truncated release history reports the check limit instead of claiming latest");
                assert(AppUpdate.SelectPreview(page=>"[]")==null,"Empty release page returns no candidate");
                assert(StartupRegistration.Command(Path.Combine(temp,"space folder","FarmMotionUI.exe")).StartsWith("\"")&&StartupRegistration.Command(Path.Combine(temp,"FarmMotionUI.exe")).EndsWith("\" --startup"),"Startup command quotes executable and passes startup mode");
                assert(AppUpdate.Hash(new byte[0])=="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","Update checksum uses SHA-256");
                using(var bytes=new MemoryStream()) {
                    using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) { var entry=zip.CreateEntry("../escape.exe"); using(var writer=new StreamWriter(entry.Open())) writer.Write("bad"); }
                    bool blocked=false; try { AppUpdate.Extract(bytes.ToArray(),Path.Combine(temp,"unpack")); } catch(IOException) { blocked=true; } assert(blocked&&!File.Exists(Path.Combine(temp,"escape.exe")),"Updater rejects archive directory traversal");
                }
                using(var bytes=new MemoryStream()) {
                    using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) { var entry=zip.CreateEntry("README.md"); using(var writer=new StreamWriter(entry.Open())) writer.Write("incomplete"); }
                    bool blocked=false; try { AppUpdate.Extract(bytes.ToArray(),Path.Combine(temp,"incomplete")); } catch(IOException) { blocked=true; } assert(blocked,"Updater rejects packages missing application files");
                }
                foreach(string bad in new[]{"app-options.json","docs/bad.exe","FarmMotionUI.exe/../../escape.exe"}) {
                    using(var bytes=new MemoryStream()) { using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) zip.CreateEntry(bad); bool blocked=false; try { AppUpdate.Extract(bytes.ToArray(),Path.Combine(temp,Guid.NewGuid().ToString("N"))); } catch(IOException) { blocked=true; } assert(blocked,"Updater rejects unexpected archive file: "+bad); }
                }
                using(var bytes=new MemoryStream()) { using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) { zip.CreateEntry("FarmMotion.exe"); zip.CreateEntry("farmmotion.exe"); } bool blocked=false; try { AppUpdate.Extract(bytes.ToArray(),Path.Combine(temp,"duplicate")); } catch(IOException) { blocked=true; } assert(blocked,"Updater rejects duplicate case-insensitive archive paths"); }
                using(var bytes=new MemoryStream()) {
                    using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)) foreach(string name in new[]{"FarmMotion.exe","FarmMotion.exe.config","SDL3.dll","SDL3-LICENSE.txt","Wpf.Ui.dll","Wpf.Ui.Abstractions.dll","System.Memory.dll","System.Buffers.dll","System.Numerics.Vectors.dll","System.Runtime.CompilerServices.Unsafe.dll","WPF-UI-LICENSE.md","MICROSOFT-RUNTIME-LICENSE.txt"}) {
                        using(var input=File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name))) using(var output=zip.CreateEntry(name).Open()) input.CopyTo(output);
                    }
                    string verified=Path.Combine(temp,"actual-package"); AppUpdate.Extract(bytes.ToArray(),verified); AppUpdate.ValidateVersion(verified,AppVersion.Display);
                    assert(File.Exists(Path.Combine(verified,"SDL3.dll"))&&Directory.GetFiles(verified,"*.exe").Length==1,"Single-executable app package extracts and passes executable version validation");
                    bool wrongVersion=false; try { AppUpdate.ValidateVersion(verified,"999.0.0"); } catch(IOException) { wrongVersion=true; } assert(wrongVersion,"Updater rejects release and executable version mismatch");
                }
                string target=Path.Combine(temp,"target"),payload=Path.Combine(temp,"payload"); Directory.CreateDirectory(target); Directory.CreateDirectory(payload); File.WriteAllText(Path.Combine(target,"FarmMotion.exe"),"old"); File.WriteAllText(Path.Combine(payload,"FarmMotion.exe"),"new"); File.WriteAllText(Path.Combine(payload,"SDL3.dll"),"new dll");
                bool rolledBack=false; try { AppUpdate.InstallPayload(payload,target,delegate { throw new IOException("simulated restart failure"); }); } catch(IOException) { rolledBack=true; }
                assert(rolledBack&&File.ReadAllText(Path.Combine(target,"FarmMotion.exe"))=="old"&&!File.Exists(Path.Combine(target,"SDL3.dll")),"Restart failure restores replaced files and removes newly installed files");
                bool restarted=false; AppUpdate.InstallPayload(payload,target,delegate { restarted=true; }); assert(restarted&&File.ReadAllText(Path.Combine(target,"FarmMotion.exe"))=="new","Successful update installs files before restart");
                assert(Directory.GetFiles(target,"*.update-*",SearchOption.AllDirectories).Length==0,"Atomic update and rollback leave no sibling staging files");
            } finally { Directory.Delete(temp,true); }
        }
    }
}
