// GPL-2.0-only. Compact WPF preferences and release updates.
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Button=Wpf.Ui.Controls.Button;
namespace FarmMotion {
    sealed class WpfOptionsWindow : Window {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        public string InstallFolder { get; private set; }
        readonly AppOptions options;
        readonly bool testing;
        readonly CheckBox startup=new CheckBox(), output=new CheckBox(), bluetooth=new CheckBox(), previews=new CheckBox();
        readonly PasswordBox token=new PasswordBox();
        readonly TextBlock notes=new TextBlock();
        readonly Button check=new Button(), install=new Button(), save=new Button(), close=new Button(), browser=new Button();
        UpdateRelease release;
        bool busy,closed;
        static Brush Color(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
        static readonly Brush Muted=UiTheme.Muted, TextColor=UiTheme.Text;
        public WpfOptionsWindow(AppOptions value,bool testing=false) {
            options=value; this.testing=testing;
            Title="FarmMotion · Settings"; Width=600; Height=600; MinWidth=440; MinHeight=440;
            WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=UiTheme.Background; Foreground=TextColor; FontFamily=new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize=14;
            UseLayoutRounding=true; SnapsToDevicePixels=true;
            try { using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)) Icon=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); } catch { }
            SourceInitialized+=delegate { try { int dark=1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref dark,sizeof(int)); } catch { } };
            var root=new DockPanel { Margin=new Thickness(20) }; Content=root;
            var title=new StackPanel { Margin=new Thickness(0,0,0,18) }; DockPanel.SetDock(title,Dock.Top); root.Children.Add(title);
            title.Children.Add(new TextBlock { Text="Settings",FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=TextColor });
            title.Children.Add(new TextBlock { Text="FarmMotion v"+AppVersion.Display,Foreground=Muted,Margin=new Thickness(0,4,0,0) });
            var footer=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            SetButton(close,"Cancel"); SetButton(save,"Save preferences"); close.Margin=new Thickness(0,0,8,0); save.Margin=new Thickness(0);
            footer.Children.Add(close); footer.Children.Add(save);
            var scroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,8,0) }; root.Children.Add(scroll);
            var body=new StackPanel(); scroll.Content=body;
            var startupCard=Card(body,"Startup");
            SetToggle(startup,"Start with Windows",testing ? false:StartupRegistration.Enabled); startupCard.Children.Add(startup);
            SetToggle(output,"Enable output when FarmMotion starts",value.EnableOutputOnStartup); startupCard.Children.Add(output);
            var controllerCard=Card(body,"Controller");
            SetToggle(bluetooth,"Allow Bluetooth PlayStation rumble",value.AllowBluetoothRumble); controllerCard.Children.Add(bluetooth);
            var updatesCard=Card(body,"Updates");
            SetToggle(previews,"Include preview releases",value.IncludePreviewUpdates); updatesCard.Children.Add(previews);
            updatesCard.Children.Add(new TextBlock { Text="GitHub token (optional)",Foreground=TextColor,Margin=new Thickness(0,12,0,0) });
            token.Margin=new Thickness(0,4,0,10); token.Padding=new Thickness(9,7,9,7); token.MinHeight=34;
            token.ToolTip="A token with Contents read permission for the FarmMotion repository."; updatesCard.Children.Add(token);
            var actions=new WrapPanel { Margin=new Thickness(0,0,0,8) }; updatesCard.Children.Add(actions);
            SetButton(check,"Check for updates"); SetButton(install,"Install and restart"); SetButton(browser,"Release page");
            install.IsEnabled=false; actions.Children.Add(check); actions.Children.Add(install); actions.Children.Add(browser);
            notes.TextWrapping=TextWrapping.Wrap; notes.Foreground=Muted; notes.LineHeight=19;
            notes.Text="Installed: v"+AppVersion.Display; updatesCard.Children.Add(notes);
            check.Click+=async delegate { await CheckForUpdates(); };
            install.Click+=async delegate { await InstallUpdate(); };
            browser.Click+=delegate { if(testing) return; try { Process.Start(new ProcessStartInfo(AppUpdate.ReleasesPage) { UseShellExecute=true }); } catch(Exception ex) { notes.Text="Could not open the release page: "+ex.Message; } };
            save.Click+=delegate { if(busy) return; try { Save(); DialogResult=true; } catch(Exception ex) { notes.Text=ex.Message; } };
            close.Click+=delegate { if(!busy) Close(); };
            previews.Checked+=delegate { InvalidateRelease(); }; previews.Unchecked+=delegate { InvalidateRelease(); };
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(busy) { e.Cancel=true; notes.Text="Please wait for the update operation to finish before closing settings."; } };
            Closed+=delegate { closed=true; token.Clear(); };
        }
        static StackPanel Card(Panel parent,string name) {
            var panel=new StackPanel();
            panel.Children.Add(new TextBlock { Text=name,FontWeight=FontWeights.SemiBold,Foreground=TextColor,FontSize=14,Margin=new Thickness(0,0,0,10) });
            parent.Children.Add(UiTheme.Card(panel)); return panel;
        }
        static void SetToggle(CheckBox control,string text,bool value) { control.Content=new TextBlock { Text=text,TextWrapping=TextWrapping.Wrap }; control.IsChecked=value; control.Margin=new Thickness(0,4,0,4); control.VerticalContentAlignment=VerticalAlignment.Center; }
        static void SetButton(Button button,string text) { UiTheme.Button(button,false); button.Content=text; button.Margin=new Thickness(0,0,8,8); }
        void InvalidateRelease() { if(!busy) { release=null; install.IsEnabled=false; } }
        void SetBusy(bool value) {
            busy=value; check.IsEnabled=save.IsEnabled=close.IsEnabled=token.IsEnabled=previews.IsEnabled=startup.IsEnabled=output.IsEnabled=bluetooth.IsEnabled=browser.IsEnabled=!value;
            install.IsEnabled=!value&&release!=null; Cursor=value ? System.Windows.Input.Cursors.Wait:null;
        }
        async Task CheckForUpdates() {
            if(busy) return;
            if(testing) { notes.Text="UI preview: update checks are disabled."; return; }
            SetBusy(true); release=null; string credential=token.Password; bool preview=previews.IsChecked==true;
            notes.Text="Checking GitHub releases…";
            try { var found=await Task.Run(()=>AppUpdate.Check(credential,preview)); if(closed) return; release=found; notes.Text=release==null ? "You have the latest version in this channel." : release.Version+Environment.NewLine+release.Notes; }
            catch(Exception ex) { if(!closed) notes.Text="Update check failed: "+ex.Message+Environment.NewLine+"A 404 can mean a private repository, missing access, or no release. Try the release page."; }
            finally { if(!closed) SetBusy(false); }
        }
        async Task InstallUpdate() {
            if(busy||release==null||testing) return;
            SetBusy(true); string credential=token.Password; UpdateRelease selected=release; notes.Text="Downloading and verifying "+selected.Version+"…";
            try { string folder=await Task.Run(()=>AppUpdate.Stage(selected,credential)); if(closed) return; Save(); InstallFolder=folder; SetBusy(false); DialogResult=true; }
            catch(Exception ex) { if(!closed) notes.Text="Update preparation failed: "+ex.Message; }
            finally { if(!closed) SetBusy(false); }
        }
        void Save() {
            if(testing) return;
            bool enableStartup=startup.IsChecked==true;
            if(enableStartup!=StartupRegistration.Enabled) {
                if(enableStartup && Path.GetFullPath(System.Windows.Forms.Application.ExecutablePath).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) {
                    if(MessageBox.Show(this,"This copy runs from a temporary folder. Windows may delete it and break startup. Register this location anyway?","Temporary app location",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) throw new IOException("Startup registration cancelled. Move FarmMotion to a permanent folder first.");
                }
                StartupRegistration.Set(enableStartup);
            }
            options.EnableOutputOnStartup=output.IsChecked==true; options.AllowBluetoothRumble=bluetooth.IsChecked==true; options.IncludePreviewUpdates=previews.IsChecked==true; options.Save();
        }
    }
}
