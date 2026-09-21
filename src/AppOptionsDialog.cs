using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace FarmMotion {
    sealed class AppOptionsDialog : Form {
        public string InstallFolder { get; private set; }
        readonly AppOptions options; readonly CheckBox startup=new CheckBox(), output=new CheckBox(), bluetooth=new CheckBox(), previews=new CheckBox();
        readonly TextBox token=new TextBox(), notes=new TextBox(); readonly Button check=new Button(), install=new Button(), save=new Button(); UpdateRelease release; bool busy;
        public AppOptionsDialog(AppOptions value) {
            options=value; Text="FarmMotion preferences · v"+AppVersion.Display; Size=new Size(640,540); MinimumSize=Size; StartPosition=FormStartPosition.CenterParent;
            var panel=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16),AutoScroll=true }; Controls.Add(panel);
            startup.Text="Start with Windows (at sign-in)"; startup.AutoSize=true; startup.Checked=StartupRegistration.Enabled; panel.Controls.Add(startup);
            panel.Controls.Add(new Label { AutoSize=true,MaximumSize=new Size(575,0),Text="Startup uses this app's current location. Keep it in a permanent folder; moving or deleting it breaks startup." });
            output.Text="Enable output when FarmMotion starts"; output.AutoSize=true; output.Checked=value.EnableOutputOnStartup; panel.Controls.Add(output);
            bluetooth.Text="Allow Bluetooth PlayStation rumble (may affect DirectInput compatibility)"; bluetooth.AutoSize=true; bluetooth.Checked=value.AllowBluetoothRumble; panel.Controls.Add(bluetooth);
            previews.Text="Include preview releases"; previews.AutoSize=true; previews.Checked=value.IncludePreviewUpdates; panel.Controls.Add(previews);
            panel.Controls.Add(new Label { Text="GitHub token for private releases (optional; kept only in this dialog)",AutoSize=true }); token.Width=575; token.UseSystemPasswordChar=true; panel.Controls.Add(token);
            var buttons=new FlowLayoutPanel { Width=575,Height=38 }; check.Text="Check for updates"; check.AutoSize=true; install.Text="Install and restart"; install.AutoSize=true; install.Enabled=false;
            buttons.Controls.Add(check); buttons.Controls.Add(install); var browser=new Button { Text="Release page",AutoSize=true }; browser.Click+=delegate { System.Diagnostics.Process.Start(AppUpdate.ReleasesPage); }; buttons.Controls.Add(browser); panel.Controls.Add(buttons);
            notes.Multiline=true; notes.ReadOnly=true; notes.Width=575; notes.Height=160; notes.ScrollBars=ScrollBars.Vertical; notes.Text="Installed version: "+AppVersion.Display+". Private repositories require a token with Contents read permission, or use the release page. Updates preserve app preferences and tuning; game mods are not installed automatically."; panel.Controls.Add(notes);
            check.Click+=async delegate { if(busy) return; SetBusy(true); release=null; string credential=token.Text; bool preview=previews.Checked; try { var found=await Task.Run(()=>AppUpdate.Check(credential,preview)); if(IsDisposed) return; release=found; notes.Text=release==null?"You have the latest version in this channel.":release.Version+Environment.NewLine+release.Notes; } catch(Exception ex) { if(!IsDisposed) notes.Text="Update check failed: "+ex.Message+"\r\nA 404 can mean a private repository, missing access, or no release. Use the release page."; } finally { if(!IsDisposed) SetBusy(false); } };
            install.Click+=async delegate { if(busy||release==null) return; SetBusy(true); string credential=token.Text; UpdateRelease selected=release; try { string folder=await Task.Run(()=>AppUpdate.Stage(selected,credential)); if(IsDisposed) return; Save(); InstallFolder=folder; SetBusy(false); DialogResult=DialogResult.OK; Close(); } catch(Exception ex) { if(!IsDisposed) notes.Text="Update preparation failed: "+ex.Message; } finally { if(!IsDisposed) SetBusy(false); } };
            save.Text="Save preferences"; save.AutoSize=true; save.Click+=delegate { if(busy) return; try { Save(); DialogResult=DialogResult.OK; Close(); } catch(Exception ex) { notes.Text=ex.Message; } }; panel.Controls.Add(save);
            previews.CheckedChanged+=delegate { if(!busy) { release=null; install.Enabled=false; } };
            FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) { e.Cancel=true; notes.Text="An update operation is in progress. Wait for it to finish before closing preferences."; } };
        }
        void SetBusy(bool value) { busy=value; check.Enabled=save.Enabled=token.Enabled=previews.Enabled=startup.Enabled=output.Enabled=bluetooth.Enabled=!value; install.Enabled=!value&&release!=null; UseWaitCursor=value; }
        void Save() {
            if(startup.Checked!=StartupRegistration.Enabled) {
                if(startup.Checked && Path.GetFullPath(Application.ExecutablePath).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) {
                    if(MessageBox.Show(this,"This copy runs from a temporary folder. Windows may delete it and break startup. Register this location anyway?","Temporary app location",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) throw new IOException("Startup registration cancelled. Move FarmMotion to a permanent folder first.");
                }
                StartupRegistration.Set(startup.Checked);
            }
            options.EnableOutputOnStartup=output.Checked; options.AllowBluetoothRumble=bluetooth.Checked; options.IncludePreviewUpdates=previews.Checked; options.Save();
        }
    }
}
