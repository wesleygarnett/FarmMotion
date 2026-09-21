// GPL-2.0-only. Dark WPF UI dashboard; output processing lives in CompanionEngine.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using UiButton=Wpf.Ui.Controls.Button;

namespace FarmMotion {
    internal sealed class WpfDashboard : Window {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        readonly CompanionEngine engine;
        readonly bool testing;
        readonly DispatcherTimer timer=new DispatcherTimer();
        readonly Grid shell=new Grid(),feedbackLayout=new Grid();
        readonly StackPanel feelColumn=new StackPanel(),monitorColumn=new StackPanel();
        readonly ScrollViewer scroll=new ScrollViewer();
        readonly UiButton enable=new UiButton();
        readonly List<UiButton> tabs=new List<UiButton>();
        readonly Dictionary<FeedbackSolo,UiButton> solos=new Dictionary<FeedbackSolo,UiButton>();
        readonly Dictionary<string,Slider> sliders=new Dictionary<string,Slider>();
        readonly ComboBox wheels=new ComboBox(),controllers=new ComboBox();
        readonly CheckBox wheelEnabled=new CheckBox(),controllerEnabled=new CheckBox(),allowReplay=new CheckBox();
        readonly TextBlock state=new TextBlock(),notice=new TextBlock(),metrics=new TextBlock(),surface=new TextBlock(),controllerStatus=new TextBlock(),deviceHint=new TextBlock(),recordingStatus=new TextBlock(),soloStatus=new TextBlock();
        readonly WpfScope scope=new WpfScope();
        readonly UiButton record=new UiButton();
        FrameworkElement feedbackPage,devicesPage,recordingsPage;
        string deviceKey=""; bool populating; int testTick;
        readonly Expander basicGroup=new Expander(),advancedGroup=new Expander();
        readonly List<Control> motionControls=new List<Control>();

        static readonly Brush Bg=UiTheme.Background,Line=UiTheme.Line,Text=UiTheme.Text,Muted=UiTheme.Muted,Accent=UiTheme.Accent;
        public WpfDashboard(bool test,bool startDisarmed=false) {
            testing=test; engine=new CompanionEngine(test,startDisarmed);
            Title="FarmMotion v"+AppVersion.Display; Width=640; Height=560; MinWidth=560; MinHeight=520;
            WindowStartupLocation=WindowStartupLocation.CenterScreen; Background=Bg; Foreground=Text; FontFamily=new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize=14; UseLayoutRounding=true;
            try { using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath)) Icon=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions()); } catch { }
            SourceInitialized+=delegate { int dark=1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle,20,ref dark,4); };
            shell.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); shell.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); shell.RowDefinitions.Add(new RowDefinition()); shell.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); shell.Background=Bg; Content=shell;
            BuildHeader();
            var navigation=new Grid { Margin=new Thickness(20,0,20,12) };
            for(int i=0;i<3;i++) navigation.ColumnDefinitions.Add(new ColumnDefinition());
            string[] names={"Feedback","Devices","Recordings"};
            for(int i=0;i<3;i++) { int selected=i; var button=Button(names[i],delegate { ShowPage(selected); }); button.HorizontalAlignment=HorizontalAlignment.Stretch; button.Margin=new Thickness(i==0?0:4,0,i==2?0:4,0); Grid.SetColumn(button,i); navigation.Children.Add(button); tabs.Add(button); }
            Grid.SetRow(navigation,1); shell.Children.Add(navigation);
            scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled; scroll.Padding=new Thickness(20,0,20,8); Grid.SetRow(scroll,2); shell.Children.Add(scroll);
            feedbackPage=BuildFeedback(); devicesPage=BuildDevices(); recordingsPage=BuildRecordings();
            var footer=new StackPanel { Margin=new Thickness(20,10,20,12) }; state.Foreground=Accent; state.FontWeight=FontWeights.SemiBold; state.TextWrapping=TextWrapping.Wrap; notice.Foreground=Muted; notice.FontSize=12; notice.TextWrapping=TextWrapping.Wrap; notice.MaxHeight=32; footer.Children.Add(state); footer.Children.Add(notice);
            var footerBorder=new Border { BorderBrush=Line,BorderThickness=new Thickness(0,1,0,0),Child=footer }; Grid.SetRow(footerBorder,3); shell.Children.Add(footerBorder);
            ShowPage(0); SizeChanged+=delegate { Reflow(); }; PreviewKeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { engine.Stop(); UpdateStatus(); e.Handled=true; } };
            timer.Interval=TimeSpan.FromMilliseconds(40); timer.Tick+=Tick;
            Loaded+=delegate { Reflow(); PopulateDevices(); UpdateStatus(); timer.Start(); };
            Closing+=delegate { engine.Stop(); if(!testing) try { engine.SaveSettings(); } catch(Exception e) { System.Windows.MessageBox.Show(this,"Settings could not be saved: "+e.Message,"FarmMotion"); } };
            Closed+=delegate { timer.Stop(); engine.Dispose(); };
        }
        static Brush BrushOf(string color) { var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); return brush; }
        static TextBlock Label(string text,double size,Brush color) { return new TextBlock { Text=text,FontSize=size<=13?14:size,Foreground=color,TextWrapping=TextWrapping.Wrap }; }
        static UiButton Button(string text,Action action) { var button=new UiButton { Content=text,MinHeight=34,Padding=new Thickness(12,6,12,6),Cursor=Cursors.Hand }; PaintButton(button,false); AutomationProperties.SetName(button,text); button.Click+=delegate { action(); }; return button; }
        static void PaintButton(UiButton button,bool primary) { UiTheme.Button(button,primary); }
        void Run(Action action) { try { action(); } catch(Exception e) { engine.Issue=e.Message; } UpdateStatus(); }
        static Wpf.Ui.Controls.Card Card(UIElement content) {
            return UiTheme.Card(content);
        }
        void BuildHeader() {
            var header=new Grid { Margin=new Thickness(20,16,20,14) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var brand=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
            brand.Children.Add(new Image { Source=UiTheme.Logo(),Width=40,Height=40,Margin=new Thickness(0,0,12,0),Stretch=Stretch.Uniform });
            var title=new StackPanel(); title.Children.Add(Label("FarmMotion",20,Text)); title.Children.Add(Label("v"+AppVersion.Display,10,Muted)); brand.Children.Add(title); header.Children.Add(brand);
            var actions=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center }; enable.Content="Enable output"; PaintButton(enable,true); enable.MinWidth=86; enable.MinHeight=36; enable.Padding=new Thickness(12,6,12,6); enable.Click+=delegate { Run(engine.ToggleOutput); }; AutomationProperties.SetName(enable,"Enable or disable all output"); enable.Margin=new Thickness(0,0,8,0); actions.Children.Add(enable);
            actions.Children.Add(Button("Settings",delegate { Run(ShowSettings); })); Grid.SetColumn(actions,1); header.Children.Add(actions); shell.Children.Add(header);
        }
        FrameworkElement BuildFeedback() {
            feedbackLayout.ColumnDefinitions.Add(new ColumnDefinition()); feedbackLayout.ColumnDefinitions.Add(new ColumnDefinition()); feedbackLayout.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); feedbackLayout.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            feedbackLayout.Children.Add(feelColumn); feedbackLayout.Children.Add(monitorColumn);
            var basic=new StackPanel(); basic.Children.Add(Tuning("strength","Wheel strength",0,FeelSettings.MaximumStrength,engine.Settings.Strength,.001,v=>(v*100).ToString("0.#")+"%",v=>engine.UpdateSettings(s=>s.Strength=v),"Maximum wheel force. Controller rumble has its own strength on Devices."));
            basic.Children.Add(Tuning("sensitivity","Movement sensitivity",.01,3,engine.Settings.Sensitivity,.01,v=>v.ToString("0.00")+"×",v=>engine.UpdateSettings(s=>s.Sensitivity=v),"How strongly vehicle movement becomes feedback."));
            basicGroup.Header="Basic"; basicGroup.Content=basic; basicGroup.IsExpanded=true; basicGroup.Margin=new Thickness(0,0,0,12); basic.Margin=new Thickness(16); feelColumn.Children.Add(basicGroup);
            var effects=new StackPanel(); soloStatus.Foreground=Accent; soloStatus.TextWrapping=TextWrapping.Wrap; soloStatus.Margin=new Thickness(0,0,0,12); effects.Children.Add(soloStatus);
            effects.Children.Add(EffectRow("bumps","Individual bumps",0,2,engine.Settings.Bumps,.01,FeedbackSolo.Bumps,v=>engine.UpdateSettings(s=>s.Bumps=v),"Suspension compression and rebound."));
            effects.Children.Add(EffectRow("body","Body movement",0,2,engine.Settings.Body,.01,FeedbackSolo.Body,v=>engine.UpdateSettings(s=>s.Body=v),"Slower movement from the vehicle body."));
            effects.Children.Add(EffectRow("texture","Fine texture",0,4,engine.Settings.Texture,.01,FeedbackSolo.Texture,v=>engine.UpdateSettings(s=>s.Texture=v),"Fast, light detail from suspension motion."));
            effects.Children.Add(EffectRow("road","Road tire buzz",0,.2,engine.Settings.RoadTexture,.0004,FeedbackSolo.Road,v=>engine.UpdateSettings(s=>s.RoadTexture=v),"Eligible agricultural tires rolling on road-class ground.",v=>(v*25).ToString("0.00")+"% of cap"));
            var advancedContent=new StackPanel { Margin=new Thickness(16) }; advancedContent.Children.Add(effects);
            var detail=new StackPanel();
            detail.Children.Add(Tuning("responseCurve","Movement response curve",1,4,engine.Settings.ResponseCurve,.1,v=>v.ToString("0.0"),v=>engine.UpdateSettings(x=>x.ResponseCurve=v),"1 is linear. Higher values quiet small movement while preserving full-scale impacts. Sensitivity sets which impacts reach full scale."));
            detail.Children.Add(Tuning("textureLimit","Fine texture limit",0,.2,engine.Settings.TextureLimit,.001,v=>(v*100).ToString("0.0")+"% of cap",v=>engine.UpdateSettings(x=>x.TextureLimit=v),"Maximum continuous fine texture, relative to wheel or controller strength. Road tire buzz has its own amount.")); detail.Children.Add(Tuning("texturePitch","Texture frequency",12,45,engine.Settings.TextureFrequency,1,v=>v.ToString("0")+" Hz",v=>engine.UpdateSettings(s=>s.TextureFrequency=v),"Wheel texture carrier rate; controller motor speed is not an exact pitch.")); motionControls.Add(sliders["texturePitch"]);
            detail.Children.Add(Tuning("roadPitch","Road buzz pitch",50,90,engine.Settings.RoadFrequency,1,v=>v.ToString("0")+" Hz",v=>engine.UpdateSettings(s=>s.RoadFrequency=v),"Independent high-frequency road layer."));
            var maintenance=new WrapPanel(); maintenance.Children.Add(Button("Save tuning",delegate { Run(engine.SaveSettings); })); var baseline=Button("Reset strength & sensitivity",delegate { sliders["strength"].Value=FeelSettings.DefaultStrength; sliders["sensitivity"].Value=.25; }); baseline.Margin=new Thickness(8,0,0,0); maintenance.Children.Add(baseline); detail.Children.Add(maintenance);
            advancedContent.Children.Add(detail); advancedGroup.Header="Advanced"; advancedGroup.Content=advancedContent; advancedGroup.Margin=new Thickness(0,0,0,12); feelColumn.Children.Add(advancedGroup);
            var live=new StackPanel(); scope.Height=150; live.Children.Add(scope); var legend=Label("Suspension • Acceleration • Preview • Output",11,Muted); legend.Margin=new Thickness(0,8,0,12); live.Children.Add(legend); metrics.Foreground=Text; metrics.TextWrapping=TextWrapping.Wrap; metrics.FontSize=12; live.Children.Add(metrics); surface.Foreground=Muted; surface.TextWrapping=TextWrapping.Wrap; surface.Margin=new Thickness(0,10,0,0); live.Children.Add(surface);
            monitorColumn.Children.Add(Card(live));
            UpdateSolo(); return feedbackLayout;
        }
        UIElement Tuning(string id,string name,double min,double max,double value,double step,Func<double,string> format,Action<double> changed,string help) {
            var panel=new StackPanel { Margin=new Thickness(0,0,0,12),ToolTip=help }; var heading=new Grid(); heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            heading.Children.Add(Label(name,13,Text)); var amount=Label(format(value),13,Accent); amount.FontWeight=FontWeights.SemiBold; Grid.SetColumn(amount,1); heading.Children.Add(amount); panel.Children.Add(heading);
            var slider=new Slider { FontSize=14, Minimum=min,Maximum=max,Value=value,SmallChange=step,LargeChange=step*5,TickFrequency=step,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,0,0),MinHeight=24 }; AutomationProperties.SetName(slider,name); slider.ValueChanged+=delegate { amount.Text=format(slider.Value); changed(slider.Value); }; sliders[id]=slider; panel.Children.Add(slider); return panel;
        }
        UIElement EffectRow(string id,string name,double min,double max,double value,double step,FeedbackSolo channel,Action<double> changed,string help,Func<double,string> formatter=null) {
            var row=new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            var tuning=Tuning(id,name,min,max,value,step,formatter??(v=>(v*100).ToString("0")+"%"),changed,help); row.Children.Add(tuning);
            var button=Button("Solo",delegate { SetSolo(channel); }); AutomationProperties.SetName(button,"Solo "+name); button.MinWidth=55; button.Margin=new Thickness(14,3,0,12); button.VerticalAlignment=VerticalAlignment.Center; button.ToolTip="Toggle solo for "+name; solos[channel]=button; Grid.SetColumn(button,1); row.Children.Add(button);
            if(channel!=FeedbackSolo.Road) { motionControls.Add(sliders[id]); motionControls.Add(button); } return row;
        }
        void SetSolo(FeedbackSolo channel) { engine.Solo=engine.Solo==channel?FeedbackSolo.All:channel; UpdateSolo(); }
        void UpdateSolo() {
            foreach(var pair in solos) { PaintButton(pair.Value,engine.Solo==pair.Key); if(pair.Key!=FeedbackSolo.Original) pair.Value.Content=engine.Solo==pair.Key?"Unsolo":"Solo"; }
            foreach(var control in motionControls) control.IsEnabled=!engine.Settings.Original;
            if(solos.ContainsKey(FeedbackSolo.Original)) solos[FeedbackSolo.Original].Visibility=engine.Settings.Original?Visibility.Visible:Visibility.Collapsed;
            soloStatus.Text=engine.Solo==FeedbackSolo.All?"":"Solo: "+SoloName(engine.Solo); soloStatus.Visibility=engine.Solo==FeedbackSolo.All?Visibility.Collapsed:Visibility.Visible;
        }
        static string SoloName(FeedbackSolo solo) { return solo==FeedbackSolo.Bumps?"Individual bumps":solo==FeedbackSolo.Body?"Body movement":solo==FeedbackSolo.Texture?"Fine texture":solo==FeedbackSolo.Road?"Road tire buzz":"Original movement"; }
        FrameworkElement BuildDevices() {
            var pagePanel=new StackPanel(); var wheel=new StackPanel(); wheelEnabled.Content="Wheel output"; wheelEnabled.IsChecked=engine.Options.WheelEnabled; wheelEnabled.Margin=new Thickness(0,0,0,12); wheelEnabled.Click+=delegate { engine.SetWheelEnabled(wheelEnabled.IsChecked==true); }; wheel.Children.Add(wheelEnabled);
            wheels.DisplayMemberPath="Name"; wheels.MinHeight=36; wheels.Margin=new Thickness(0,0,0,12); wheels.SelectionChanged+=delegate { if(!populating) { var item=wheels.SelectedItem as Wheel.Info; engine.SelectWheel(item==null?Guid.Empty:item.Id); } }; AutomationProperties.SetName(wheels,"Force feedback wheel"); wheel.Children.Add(wheels);
            wheel.Children.Add(Button("Refresh wheels",delegate { Run(engine.RefreshWheels); PopulateDevices(); })); pagePanel.Children.Add(Card(wheel));
            var pad=new StackPanel(); controllerEnabled.Content="Controller rumble"; controllerEnabled.IsChecked=engine.Options.ControllerEnabled; controllerEnabled.Margin=new Thickness(0,0,0,12); controllerEnabled.Click+=delegate { engine.SetControllerEnabled(controllerEnabled.IsChecked==true); }; pad.Children.Add(controllerEnabled);
            controllers.DisplayMemberPath="Name"; controllers.MinHeight=36; controllers.Margin=new Thickness(0,0,0,12); controllers.SelectionChanged+=delegate { if(!populating) { var item=controllers.SelectedItem as ControllerOutput.Info; engine.SelectController(item==null?"":item.Id); } }; AutomationProperties.SetName(controllers,"Rumble controller"); pad.Children.Add(controllers);
            pad.Children.Add(Tuning("rumble","Rumble strength",0,1,engine.Options.ControllerStrength,.01,v=>(v*100).ToString("0")+"%",v=>{lock(engine.Sync) engine.Options.ControllerStrength=v;},"Independent from wheel strength.")); pad.Children.Add(Button("Refresh controllers",delegate { engine.RefreshControllers(); }));
            controllerStatus.Foreground=Accent; controllerStatus.TextWrapping=TextWrapping.Wrap; controllerStatus.Margin=new Thickness(0,12,0,0); pad.Children.Add(controllerStatus); pagePanel.Children.Add(Card(pad));
            deviceHint.Foreground=Muted; deviceHint.TextWrapping=TextWrapping.Wrap; pagePanel.Children.Add(deviceHint); return pagePanel;
        }
        FrameworkElement BuildRecordings() {
            var pagePanel=new StackPanel(); var capture=new StackPanel();  PaintButton(record,false); record.Content="Record drive"; record.MinHeight=36; record.Margin=new Thickness(0,14,0,0); record.Click+=delegate { Run(RecordDrive); }; capture.Children.Add(record); recordingStatus.Foreground=Accent; recordingStatus.Margin=new Thickness(0,12,0,0); recordingStatus.TextWrapping=TextWrapping.Wrap; capture.Children.Add(recordingStatus); pagePanel.Children.Add(Card(capture));
            var replay=new StackPanel(); var buttons=new WrapPanel(); buttons.Children.Add(Button("Load replay",delegate { Run(LoadReplay); })); var again=Button("Replay again",delegate { Run(engine.RestartReplay); }); again.Margin=new Thickness(8,0,0,8); buttons.Children.Add(again); var live=Button("Return to live",delegate { engine.ReturnToLive(); UpdateStatus(); }); live.Margin=new Thickness(8,0,0,8); buttons.Children.Add(live); replay.Children.Add(buttons);
            allowReplay.Content="Allow device output during replay"; allowReplay.Margin=new Thickness(0,8,0,0); allowReplay.Click+=delegate { engine.AllowReplay=allowReplay.IsChecked==true; engine.Armed=false; }; replay.Children.Add(allowReplay); pagePanel.Children.Add(Card(replay)); return pagePanel;
        }
        void RecordDrive() { if(engine.Recording) { engine.StopRecording(); return; } if(testing) return; var dialog=new SaveFileDialog { Filter="FarmMotion recording|*.fmr",FileName="drive.fmr" }; if(dialog.ShowDialog(this)==true) engine.Record(dialog.FileName); }
        void LoadReplay() { engine.Armed=false; if(testing) return; var dialog=new OpenFileDialog { Filter="FarmMotion recording|*.fmr" }; if(dialog.ShowDialog(this)==true) engine.LoadReplay(dialog.FileName); }
        void ShowSettings() {
            var dialog=new WpfOptionsWindow(engine.Options,testing) { Owner=this };
            if(dialog.ShowDialog()==true) { engine.ApplyOptions(); if(dialog.InstallFolder!=null) { engine.Stop(); AppUpdate.BeginInstall(dialog.InstallFolder); Close(); } }
        }
        void ShowPage(int index) { scroll.Content=index==0?feedbackPage:index==1?devicesPage:recordingsPage; foreach(var tab in tabs) PaintButton(tab,tabs.IndexOf(tab)==index); scroll.ScrollToTop(); Reflow(); }
        void Reflow() {
            bool wide=ActualWidth>=900;
            feedbackLayout.ColumnDefinitions[0].Width=new GridLength(wide?1.3:1,GridUnitType.Star); feedbackLayout.ColumnDefinitions[1].Width=wide?new GridLength(1,GridUnitType.Star):new GridLength(0);
            Grid.SetRow(feelColumn,0); Grid.SetColumn(feelColumn,0); Grid.SetRow(monitorColumn,wide?0:1); Grid.SetColumn(monitorColumn,wide?1:0); feelColumn.Margin=new Thickness(0,0,wide?12:0,0);
        }
        void PopulateDevices() {
            populating=true;
            try { wheels.Items.Clear(); foreach(var item in engine.Wheels) { wheels.Items.Add(item); if(item.Id==engine.SelectedWheel) wheels.SelectedItem=item; } controllers.Items.Clear(); foreach(var item in engine.Controllers) if(item.SupportsRumble) { controllers.Items.Add(item); if(item.Id==engine.SelectedController) controllers.SelectedItem=item; }
                wheels.IsEnabled=wheels.Items.Count>0; controllers.IsEnabled=controllers.Items.Count>0;
                if(wheels.Items.Count==0) { wheels.Items.Add(new Wheel.Info { Name="No wheel detected",Id=Guid.Empty }); wheels.SelectedIndex=0; }
                if(controllers.Items.Count==0) { controllers.Items.Add(new ControllerOutput.Info { Name="No rumble controller detected",Id="",SupportsRumble=false }); controllers.SelectedIndex=0; }
                deviceHint.Text="";
            } finally { populating=false; }
        }
        void UpdateStatus() {
            enable.Content=engine.Armed?"Disable output":"Enable output"; enable.Appearance=engine.Armed?Wpf.Ui.Controls.ControlAppearance.Danger:Wpf.Ui.Controls.ControlAppearance.Primary;
            state.Text=(engine.Replaying?"REPLAY":"LIVE")+"  /  "+(engine.Armed?"Output enabled":"Output off")+(engine.Solo!=FeedbackSolo.All?"  /  SOLO: "+SoloName(engine.Solo):"");
            notice.Text=string.IsNullOrEmpty(engine.Issue)?engine.TelemetryStatus:engine.Issue;
            notice.ToolTip=notice.Text;
            metrics.Text=string.Format("Wheel preview  {0:+0.00;-0.00;0.00}%\nCommanded  {1:+0.00;-0.00;0.00}%   {2}\nLoop  {3:0} Hz  /  longest {4:0.0} ms",engine.Preview*100,engine.Commanded*100,engine.Clipped?"LIMITING":"",engine.OutputHz,engine.OutputGapMs);
            metrics.Text+="\nSuspension  "+engine.Suspension.ToString("0.000")+"  /  Acceleration  "+engine.Acceleration.ToString("0.000"); surface.Text=engine.Vehicle+"\n"+engine.SurfaceStatus; controllerStatus.Text=engine.ControllerStatus; record.Content=engine.Recording?"Stop recording":"Record drive"; recordingStatus.Text=engine.Recording?"Recording live telemetry":engine.Replaying?"Playing recorded telemetry":"Ready to record";
        }
        void Tick(object sender,EventArgs e) {
            engine.Focused=IsActive; engine.Tick(); string key=""; foreach(var item in engine.Wheels) key+=item.Id+";"; foreach(var item in engine.Controllers) key+=item.Id+"="+item.SupportsRumble+";"; if(key!=deviceKey) { deviceKey=key; PopulateDevices(); }
            foreach(var frame in engine.DrainScopeFrames()) scope.Add(frame.Suspension,frame.Acceleration,frame.Force,frame.Preview);
            if(testing) { testTick++; scope.Add(Math.Sin(testTick*.19)*.45,Math.Sin(testTick*.11)*.3,Math.Sin(testTick*.27)*.25,Math.Sin(testTick*.27)*.3); }
            scope.InvalidateVisual(); UpdateStatus(); if(testing) RunUiChecks();
        }
        void Capture(string name) {
            UpdateLayout(); var dpi=VisualTreeHelper.GetDpi(shell); var image=new RenderTargetBitmap((int)Math.Ceiling(shell.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(shell.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32); image.Render(shell); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name))) png.Save(file);
        }
        void Check(bool condition,string text) { if(!condition) throw new InvalidOperationException("WPF UI test: "+text); }
        void CheckCompact() {
            UpdateLayout(); Check(scroll.ScrollableWidth<1,"horizontal scrolling at "+Width); var bounds=enable.TransformToAncestor(shell).TransformBounds(new Rect(enable.RenderSize)); Check(bounds.Left>=0&&bounds.Right<=shell.ActualWidth&&bounds.Bottom<=shell.ActualHeight,"output toggle clipped"); Check(enable.IsVisible,"output controls not visible");
        }
        void RunUiChecks() {
            if(testTick==3) { Check(!engine.Armed,"test mode armed hardware"); Check(typeof(UiButton).Assembly.GetName().Name=="Wpf.Ui","real WPF UI library"); Check(Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme()==Wpf.Ui.Appearance.ApplicationTheme.Dark && enable.ReadLocalValue(Control.BackgroundProperty)==DependencyProperty.UnsetValue,"framework dark theme overridden"); Width=960; Height=760; }
            if(testTick==7) { CheckCompact(); Capture("wpf-dashboard-wide.png"); Check(tabs.Count==3 && !sliders["texture"].IsVisible,"Basic contains advanced controls"); var before=engine.Settings.Copy(); SetSolo(FeedbackSolo.Bumps); Check(engine.Solo==FeedbackSolo.Bumps&&engine.Settings.Texture==before.Texture&&engine.Settings.Bumps==before.Bumps,"solo changed tuning"); SetSolo(FeedbackSolo.Bumps); Check(engine.Solo==FeedbackSolo.All,"solo toggle failed"); }
            if(testTick==10) { Width=640; Height=600; }
            if(testTick==14) { CheckCompact(); Capture("wpf-dashboard-compact.png"); advancedGroup.IsExpanded=true; UpdateLayout(); Check(engine.Settings.Enhanced&&!engine.Settings.Original,"dashboard defaults to V2"); }
            if(testTick==18) { Check(sliders["texture"].IsVisible,"advanced expander did not open"); CheckCompact(); Capture("wpf-advanced-compact.png"); var value=engine.Settings.Texture; basicGroup.IsExpanded=false; advancedGroup.IsExpanded=false; UpdateLayout(); Check(!basicGroup.IsExpanded&&!advancedGroup.IsExpanded&&engine.Settings.Texture==value,"collapse changes tuning"); basicGroup.IsExpanded=true; ShowPage(1); }
            if(testTick==22) { CheckCompact(); Capture("wpf-devices-compact.png"); ShowPage(2); }
            if(testTick==26) { CheckCompact(); Capture("wpf-recordings-compact.png"); ShowPage(0); Width=560; Height=520; }
            if(testTick==30) { CheckCompact(); Capture("wpf-dashboard-small.png"); scroll.ScrollToBottom(); }
            if(testTick==34) { CheckCompact(); Capture("wpf-dashboard-small-scrolled.png"); var options=new WpfOptionsWindow(new AppOptions(),true) { Owner=this,Width=480,Height=560 }; options.Show(); options.UpdateLayout(); var target=new RenderTargetBitmap((int)options.ActualWidth,(int)options.ActualHeight,96,96,PixelFormats.Pbgra32); target.Render(options); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(target)); using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wpf-settings-compact.png"))) png.Save(file); options.Close(); }
            if(testTick==38) { enable.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Check(engine.Armed&&enable.Content.ToString()=="Disable output","Enable click or label failed"); enable.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Check(!engine.Armed&&engine.Commanded==0&&enable.Content.ToString()=="Enable output","Disable click or label failed"); enable.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(this),0,Key.Escape) { RoutedEvent=Keyboard.PreviewKeyDownEvent }); Check(!engine.Armed&&enable.Content.ToString()=="Enable output","Escape failed"); engine.RefreshWheels(); engine.RefreshControllers(); Check(!engine.Armed,"refresh undid disable"); File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dashboard-ui-test.txt"),"Feedback, Devices and Recordings; Basic/Advanced expanders; WPF UI dark dictionaries. Wide 960x760, compact 640x600, small 560x520: no horizontal scroll; output toggle remains visible. Solo, V2 processing, settings and pages verified without hardware output."); Close(); }
        }
    }
    internal sealed class WpfScope : FrameworkElement {
        struct Frame { public double Motion,Acceleration,Force,Preview; }
        readonly Frame[] frames=new Frame[500]; int head,count;
        public void Add(double suspension,double acceleration,double force,double preview) { frames[head]=new Frame { Motion=suspension,Acceleration=acceleration,Force=force,Preview=preview }; head=(head+1)%frames.Length; count=Math.Min(count+1,frames.Length); }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc); double w=ActualWidth,h=ActualHeight; dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(22,26,34)),null,new Rect(0,0,w,h),6,6);
            var grid=new Pen(new SolidColorBrush(Color.FromRgb(44,51,63)),1); for(int i=1;i<4;i++) dc.DrawLine(grid,new Point(0,h*i/4),new Point(w,h*i/4)); if(count<2) return;
            Color[] colors={Color.FromRgb(121,223,204),Color.FromRgb(189,160,245),Color.FromRgb(91,112,142),Color.FromRgb(239,182,108)};
            for(int channel=3;channel>=0;channel--) { var geometry=new StreamGeometry(); using(var ctx=geometry.Open()) { for(int i=0;i<count;i++) { var f=frames[(head-count+i+frames.Length)%frames.Length]; double v=channel==0?f.Motion:channel==1?f.Force:channel==2?f.Preview:f.Acceleration; var point=new Point(w*i/(frames.Length-1),h/2-Math.Max(-1,Math.Min(1,v))*(h/2-8)); if(i==0) ctx.BeginFigure(point,false,false); else ctx.LineTo(point,true,false); } } geometry.Freeze(); dc.DrawGeometry(null,new Pen(new SolidColorBrush(colors[channel]),channel==2?1:1.5),geometry); }
        }
    }
}
