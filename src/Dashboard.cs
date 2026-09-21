// GPL-2.0-only. Native Windows tuning dashboard.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace FarmMotion {
    public struct ScopeFrame {
        public float Suspension,Acceleration,Force,Preview;
        public ScopeFrame(double suspension,double acceleration,double force,double preview) { Suspension=(float)suspension; Acceleration=(float)acceleration; Force=(float)force; Preview=(float)preview; }
        public float Channel(int index) { return index==0 ? Suspension : index==1 ? Acceleration : index==2 ? Force : Preview; }
    }
    public sealed class Scope : Control {
        readonly ScopeFrame[] frames=new ScopeFrame[1000]; int head,count;
        public Scope() { DoubleBuffered=true; BackColor=Color.FromArgb(18,26,36); Height=210; Dock=DockStyle.Fill; }
        public void Add(double suspension,double acceleration,double force,double preview) { frames[head]=new ScopeFrame(suspension,acceleration,force,preview); head=(head+1)%frames.Length; count=Math.Min(count+1,frames.Length); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g=e.Graphics; float mid=Height/2f; float scale=g.DpiX/96f; g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var pen=new Pen(Color.FromArgb(50,65,80))) { for(int i=1;i<4;i++) g.DrawLine(pen,0,Height*i/4,Width,Height*i/4); }
            Color[] colors={Color.Turquoise,Color.Goldenrod,Color.Orchid,Color.SteelBlue}; int start=(head-count+frames.Length)%frames.Length;
            for(int channel=3;channel>=0;channel--) using(var pen=new Pen(colors[channel],(channel==3 ? 1f : 1.7f)*scale)) {
                for(int i=1;i<count;i++) g.DrawLine(pen,(i-1)*Width/999f,mid-Math.Max(-1,Math.Min(1,frames[(start+i-1)%frames.Length].Channel(channel)))*(mid-12*scale),i*Width/999f,mid-Math.Max(-1,Math.Min(1,frames[(start+i)%frames.Length].Channel(channel)))*(mid-12*scale));
            }
        }
    }
    public sealed class Dashboard : Form {
        [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr first,IntPtr second);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
        readonly object gate=new object(); readonly Stopwatch clock=Stopwatch.StartNew();
        readonly Feel feel=new Feel(); FeelSettings settings=new FeelSettings();
        readonly Queue<ScopeFrame> scopeFrames=new Queue<ScopeFrame>(1000);
        readonly bool testing; Receiver receiver; Thread worker; volatile bool closing;
        bool armed, replaying, allowReplay, focused; Guid selected;
        double commanded, preview, recordStart, replayStart; int replayIndex;
        double suspension,acceleration; bool clipped; string vehicle="No active vehicle", issue="";
        double outputHz,outputGapMs;
        List<RecordedSample> replay; RecordingWriter recorder;
        readonly Scope scope=new Scope(); readonly Label status=new Label(), values=new Label(), message=new Label();
        readonly Label surfaceStatus=new Label();
        readonly ComboBox wheels=new ComboBox(), mode=new ComboBox(); readonly CheckBox replayForces=new CheckBox();
        readonly Button arm=new Button(), record=new Button();
        readonly Button advancedToggle=new Button();
        TableLayoutPanel tuningControls; bool advancedVisible;
        readonly List<TrackBar> sliders=new List<TrackBar>();
        readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmMotion","settings.json");
        int testTicks;
        public Dashboard(bool test) {
            testing=test; SuspendLayout(); AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            Text="FarmMotion | Force feedback & rumble for Farm Simulator"; Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); ClientSize=new Size(1180,800); MinimumSize=new Size(760,580);
            BackColor=Color.FromArgb(243,246,248); ForeColor=Color.FromArgb(27,43,54); Font=new Font("Segoe UI",10);
            StartPosition=FormStartPosition.CenterScreen;
            if(!testing) try { settings=SettingsStore.Load(settingsPath); } catch { issue="Saved settings could not be loaded; using baseline."; }
            settings.Validate();
            var viewport=new Panel { Dock=DockStyle.Fill,AutoScroll=true };
            Controls.Add(viewport);
            var layout=new TableLayoutPanel { ColumnCount=1,RowCount=2,Padding=new Padding(16,12,16,16),MinimumSize=new Size(1080,700) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            viewport.Controls.Add(layout);
            viewport.ClientSizeChanged+=delegate {
                layout.Size=new Size(Math.Max(layout.MinimumSize.Width,viewport.ClientSize.Width-SystemInformation.VerticalScrollBarWidth),Math.Max(layout.MinimumSize.Height,viewport.ClientSize.Height-SystemInformation.HorizontalScrollBarHeight));
            };
            var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.Controls.Add(header,0,0);
            header.Controls.Add(new Label { Text="FarmMotion",Font=new Font(Font.FontFamily,20,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty },0,0);
            var content=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,10,0,0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));
            content.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.Controls.Add(content,0,1);
            var tuning=Card("Vehicle feel"); var monitor=Card("Live monitor");
            tuning.Margin=new Padding(0,0,10,0); monitor.Margin=new Padding(10,0,0,0);
            content.Controls.Add(tuning,0,0); content.Controls.Add(monitor,1,0);
            var top=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,4,0,0),Margin=Padding.Empty };
            wheels.Width=300; wheels.DisplayMember="Name"; wheels.DropDownStyle=ComboBoxStyle.DropDownList; wheels.SelectedIndexChanged+=delegate { lock(gate) { selected=wheels.SelectedItem is Wheel.Info ? ((Wheel.Info)wheels.SelectedItem).Id : Guid.Empty; armed=false; } };
            top.Controls.Add(wheels); top.Controls.Add(Button("Refresh wheels",RefreshWheels));
            arm.Text="Enable output"; StyleButton(arm,true); arm.Click+=delegate { lock(gate) { armed=!armed; issue=""; } }; top.Controls.Add(arm);
            var stop=Button("STOP",delegate { lock(gate) { armed=false; replaying=false; feel.Reset(); } }); stop.BackColor=Color.FromArgb(254,233,232); stop.ForeColor=Color.FromArgb(164,39,43); top.Controls.Add(stop); header.Controls.Add(top,1,0);
            wheels.Margin=new Padding(0,5,12,0);
            foreach(Control control in top.Controls) if(control is Button) { control.AutoSize=false; control.MinimumSize=Size.Empty; control.Size=new Size(control==stop ? 76:132,36); control.Padding=Padding.Empty; control.Margin=new Padding(0,0,8,0); }
            var controls=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=12,Margin=Padding.Empty }; tuningControls=controls;
            for(int row=0;row<11;row++) controls.RowStyles.Add(new RowStyle(SizeType.Absolute,row==0 ? 28 : (row==3 ? 36 : 40)));
            controls.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            controls.Controls.Add(new Label { Text="Basic mode",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,10,FontStyle.Bold),ForeColor=Color.FromArgb(14,111,106) },0,0);
            controls.SetColumnSpan(controls.GetControlFromPosition(0,0),4);
            StyleButton(advancedToggle,false); advancedToggle.Dock=DockStyle.Fill; advancedToggle.MinimumSize=Size.Empty; advancedToggle.Padding=Padding.Empty; advancedToggle.TextAlign=ContentAlignment.MiddleLeft; advancedToggle.Margin=new Padding(0,3,0,3);
            advancedToggle.Click+=delegate { SetAdvanced(!advancedVisible); };
            controls.Controls.Add(advancedToggle,0,3); controls.SetColumnSpan(advancedToggle,4);
            controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,30));
            AddSlider(controls,1,"Maximum strength",0,100,(int)Math.Round(settings.Strength*1000),v=>v/10.0+" %",v=>settings.Strength=v/1000.0);
            AddSlider(controls,2,"Movement sensitivity",25,300,(int)(settings.Sensitivity*100),v=>(v/100.0).ToString("0.00")+" x",v=>settings.Sensitivity=v/100.0);
            AddSlider(controls,4,"Fine texture",0,400,(int)(settings.Texture*100),v=>v+" %",v=>settings.Texture=v/100.0);
            AddSlider(controls,5,"Individual bumps",0,200,(int)(settings.Bumps*100),v=>v+" %",v=>settings.Bumps=v/100.0);
            AddSlider(controls,6,"Body movement",0,200,(int)(settings.Body*100),v=>v+" %",v=>settings.Body=v/100.0);
            AddSlider(controls,7,"Texture frequency",12,45,(int)settings.TextureFrequency,v=>v+" Hz",v=>settings.TextureFrequency=v);
            AddSlider(controls,8,"Road tyre buzz (0 = off)",0,20,(int)Math.Round(settings.RoadTexture*100),v=>(v*.25).ToString("0.##")+" % of cap",v=>settings.RoadTexture=v/100.0);
            AddSlider(controls,9,"Road buzz pitch",50,90,(int)settings.RoadFrequency,v=>v+" Hz",v=>settings.RoadFrequency=v);
            mode.DropDownStyle=ComboBoxStyle.DropDownList; mode.Items.AddRange(new object[]{"Motion-shaped v1 (previous)","Motion-shaped v2 (new)","Original 18 Hz"}); mode.SelectedIndex=settings.Original ? 2:(settings.Enhanced ? 1:0); mode.Dock=DockStyle.Fill; mode.Margin=new Padding(3,8,3,0);
            mode.SelectedIndexChanged+=delegate { lock(gate) { settings.Original=mode.SelectedIndex==2; settings.Enhanced=mode.SelectedIndex==1; feel.Reset(); issue=mode.SelectedIndex==1 ? "V2: per-wheel bumps, layered texture, gradual peak compression." : "Previous processing restored. Sliders are shared between comparison modes."; } for(int i=2;i<6;i++) sliders[i].Enabled=mode.SelectedIndex!=2; };
            for(int i=2;i<6;i++) sliders[i].Enabled=!settings.Original;
            controls.Controls.Add(new Label { Text="Comparison",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,10); controls.Controls.Add(mode,1,10); controls.SetColumnSpan(mode,3); tuning.Controls.Add(controls,0,1); SetAdvanced(false);
            var live=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty };
            live.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            live.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            live.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
            live.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
            live.RowStyles.Add(new RowStyle(SizeType.Absolute,154));
            monitor.Controls.Add(live,0,1);
            live.Controls.Add(scope,0,0); scope.Margin=Padding.Empty;
            live.Controls.Add(new Label { Text="Teal  Suspension / 0.45 m/s     Gold  Vertical / 6 m/s²\nPurple  Commanded / 10%     Blue  Preview / 10%",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Color.FromArgb(91,108,120),Font=new Font(Font.FontFamily,9) },0,1);
            values.Dock=DockStyle.Fill; values.Font=new Font(Font.FontFamily,10,FontStyle.Bold); live.Controls.Add(values,0,2);
            var actions=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Padding=new Padding(0,10,0,0) };
            for(int i=0;i<3;i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
            for(int i=0;i<2;i++) actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            actions.Controls.Add(Button("Save settings",SaveSettings)); actions.Controls.Add(Button("7% / 0.25 baseline",delegate { sliders[0].Value=70; sliders[1].Value=25; }));
            record.Text="Record drive"; StyleButton(record,false); record.Click+=delegate { Record(); }; actions.Controls.Add(record);
            actions.Controls.Add(Button("Load replay",LoadReplay)); actions.Controls.Add(Button("Replay again",RestartReplay)); actions.Controls.Add(Button("Return to live",delegate { lock(gate) { replaying=false; armed=false; feel.Reset(); } }));
            foreach(Control action in actions.Controls) { action.Dock=DockStyle.Fill; action.Font=new Font(Font.FontFamily,9); action.Padding=new Padding(2); action.MinimumSize=Size.Empty; }
            tuning.Controls.Add(actions,0,2);
            var bottom=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false };
            replayForces.Text="Allow wheel output during replay (starts disarmed)"; replayForces.AutoSize=true; replayForces.CheckedChanged+=delegate { lock(gate) { allowReplay=replayForces.Checked; armed=false; } };
            bottom.Controls.Add(replayForces); status.AutoSize=true; message.AutoSize=true; bottom.Controls.Add(status); bottom.Controls.Add(message); live.Controls.Add(bottom,0,3);
            surfaceStatus.AutoSize=true; bottom.Controls.Add(surfaceStatus);
            bottom.SizeChanged+=delegate { foreach(Control item in bottom.Controls) item.MaximumSize=new Size(Math.Max(1,bottom.ClientSize.Width-8),0); };
            monitor.RowStyles[2].Height=0;
            ResumeLayout(true);
            KeyPreview=true; KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.KeyCode==Keys.Escape) lock(gate) armed=false; };
            if(!testing) { receiver=new Receiver(clock); receiver.SampleReceived+=OnSample; RefreshWheels(); worker=new Thread(OutputLoop) { IsBackground=true }; worker.SetApartmentState(ApartmentState.STA); worker.Start(); }
            else { PopulateWheels(new Wheel.Info[0]); }
            timer.Interval=25; timer.Tick+=Tick; timer.Start();
            FormClosing+=delegate { if(!testing) try { SaveSettings(); } catch(Exception e) { MessageBox.Show(this,"Settings could not be saved: "+e.Message,"FarmMotion",MessageBoxButtons.OK,MessageBoxIcon.Warning); } };
            FormClosed+=delegate { closing=true; timer.Stop(); if(receiver!=null) receiver.Dispose(); if(worker!=null) worker.Join(1500); lock(gate) { if(recorder!=null) recorder.Dispose(); } };
        }
        static void StyleButton(Button b,bool primary) { b.AutoSize=true; b.MinimumSize=new Size(100,36); b.Padding=new Padding(10,5,10,5); b.Margin=new Padding(0,0,8,8); b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderSize=1; b.FlatAppearance.BorderColor=Color.FromArgb(210,222,227); b.BackColor=primary ? Color.FromArgb(14,111,106):Color.White; b.ForeColor=primary ? Color.White:Color.FromArgb(27,43,54); b.Cursor=Cursors.Hand; }
        TableLayoutPanel Card(string title) {
            var card=new TableLayoutPanel { Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(18),ColumnCount=1,RowCount=3 };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); card.RowStyles.Add(new RowStyle(SizeType.Absolute,42)); card.RowStyles.Add(new RowStyle(SizeType.Percent,100)); card.RowStyles.Add(new RowStyle(SizeType.Absolute,110));
            card.Controls.Add(new Label { Text=title,Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,14,FontStyle.Bold),Margin=Padding.Empty },0,0); return card;
        }
        Button Button(string text,Action action) { var b=new Button { Text=text }; StyleButton(b,false); b.Click+=delegate { try { action(); } catch(Exception e) { lock(gate) issue=e.Message; } }; return b; }
        void SetAdvanced(bool visible) {
            advancedVisible=visible; tuningControls.SuspendLayout();
            for(int row=4;row<=10;row++) {
                tuningControls.RowStyles[row].Height=visible ? tuningControls.RowStyles[1].Height*(row==10 ? 1f:.9f) : 0;
                foreach(Control control in tuningControls.Controls) if(tuningControls.GetRow(control)==row) control.Visible=visible;
            }
            advancedToggle.Text=visible ? "Advanced mode   − Hide" : "Advanced mode   + Show all sliders";
            tuningControls.ResumeLayout(true);
        }
        static readonly string[] SliderHelp={
            "Sets the maximum wheel force for all effects. Raise it for stronger feedback; lower it for a lighter feel. 0% turns the force down to zero. The app caps this at 10% of the wheel's available force.",
            "Controls how strongly vehicle movement is translated into feedback. Raise it to pick up smaller movements; lower it for a calmer ride. Maximum strength still limits the final force.",
            "Adjusts fast, fine vibration from suspension movement. Raise it for more small detail; set it to 0% to remove this layer. It does not change the separate road tyre buzz. Not used in Original 18 Hz mode.",
            "Adjusts feedback from individual suspension bumps and rebounds. Raise it for more pronounced bumps; lower it to soften them. Not used in Original 18 Hz mode.",
            "Adjusts slower feedback from the vehicle body's vertical movement, pitch and roll. Raise it for more body motion; lower it for a steadier feel. Not used in Original 18 Hz mode.",
            "Sets the vibration rate of the fine texture layer, from 12 to 45 cycles per second (Hz). Higher values feel faster; lower values feel slower. This does not change large-bump timing or road buzz pitch. Not used in Original 18 Hz mode.",
            "Adds a light tyre buzz while agricultural tyres roll on a detected road surface. The value is a percentage of your Maximum strength cap. Set it to 0 to turn this layer off. It fades when the vehicle stops, contact is lost, or telemetry becomes stale.",
            "Sets the separate road tyre buzz rate, from 50 to 90 cycles per second (Hz). Higher values give a faster buzz. This only has an effect when Road tyre buzz is above zero and its road/tyre conditions are met."
        };
        void AddSlider(TableLayoutPanel table,int row,string name,int min,int max,int value,Func<int,string> format,Action<int> change) {
            var slider=new TrackBar { Minimum=min,Maximum=max,Value=value,Dock=DockStyle.Fill,TickStyle=TickStyle.None,SmallChange=1,LargeChange=5,AutoSize=false,Margin=new Padding(3,8,3,0),AccessibleName=name };
            var label=new Label { Text=format(value),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,ForeColor=Color.FromArgb(14,111,106),Font=new Font(Font.FontFamily,10,FontStyle.Bold) };
            slider.ValueChanged+=delegate { lock(gate) change(slider.Value); label.Text=format(slider.Value); };
            table.Controls.Add(new Label { Text=name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,row); table.Controls.Add(slider,1,row); table.Controls.Add(label,2,row);
            string explanation=SliderHelp[sliders.Count];
            var help=new Button { Text="?",Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,Margin=new Padding(3,6,0,6),Padding=Padding.Empty,AccessibleName="Explain "+name,Cursor=Cursors.Hand,ForeColor=Color.FromArgb(14,111,106) };
            help.FlatAppearance.BorderColor=Color.FromArgb(210,222,227);
            help.Click+=delegate { MessageBox.Show(this,explanation,name,MessageBoxButtons.OK,MessageBoxIcon.Information); };
            table.Controls.Add(help,3,row); sliders.Add(slider);
        }
        void SavePreview(string name) { using(var bitmap=new Bitmap(Width,Height)) { DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)); } }
        void RefreshWheels() { lock(gate) { armed=false; selected=Guid.Empty; } using(var wheel=new Wheel()) PopulateWheels(wheel.List()); }
        void PopulateWheels(IEnumerable<Wheel.Info> devices) {
            lock(gate) { armed=false; selected=Guid.Empty; }
            wheels.Items.Clear(); foreach(var item in devices) wheels.Items.Add(item);
            if(wheels.Items.Count==0) { wheels.Items.Add("no device detected"); wheels.SelectedIndex=0; arm.Enabled=false; return; }
            int index=0; for(int i=0;i<wheels.Items.Count;i++) if(wheels.Items[i].ToString().IndexOf("R3",StringComparison.OrdinalIgnoreCase)>=0) index=i;
            wheels.SelectedIndex=index; arm.Enabled=true;
        }
        void SaveSettings() { FeelSettings snapshot; lock(gate) snapshot=settings.Copy(); SettingsStore.Save(settingsPath,snapshot); lock(gate) issue="Settings saved."; }
        void OnSample(Sample sample,string json,double now) {
            lock(gate) {
                if(recorder!=null) try { recorder.WriteLine(Recording.Line(now-recordStart,json)); } catch(Exception e) { issue="Recording stopped: "+e.Message; recorder.Dispose(); recorder=null; }
                if(!replaying) { feel.Push(sample,now,settings); vehicle=sample.Active ? "Vehicle "+sample.Vehicle : "No active vehicle / paused"; }
            }
        }
        void Record() {
            lock(gate) { if(recorder!=null) { recorder.Dispose(); issue=recorder.Error==null ? "Recording saved." : "Recording stopped: "+recorder.Error; recorder=null; return; } if(replaying) { issue="Return to live before recording."; return; } }
            using(var dialog=new SaveFileDialog { Filter="FarmMotion recording|*.fmr",FileName="drive.fmr" }) if(dialog.ShowDialog(this)==DialogResult.OK) lock(gate) { recorder=new RecordingWriter(dialog.FileName); recordStart=clock.Elapsed.TotalSeconds; issue="Recording live telemetry."; }
        }
        void LoadReplay() {
            lock(gate) armed=false;
            using(var dialog=new OpenFileDialog { Filter="FarmMotion recording|*.fmr" }) if(dialog.ShowDialog(this)==DialogResult.OK) { var loaded=Recording.Load(dialog.FileName); lock(gate) replay=loaded; RestartReplay(); }
        }
        void RestartReplay() { lock(gate) { if(replay==null) { issue="Load a recording first."; return; } if(recorder!=null) { recorder.Dispose(); recorder=null; } armed=false; replaying=true; replayIndex=0; replayStart=clock.Elapsed.TotalSeconds; feel.Reset(); issue="Replay loaded. Enable output separately to feel it."; } }
        void OutputLoop() {
            Wheel wheel=null; Guid opened=Guid.Empty;
            bool preciseTimer=timeBeginPeriod(1)==0;
            double measuredStart=clock.Elapsed.TotalSeconds,lastUpdate=measuredStart,longest=0; int updates=0;
            try {
                while(!closing) {
                    bool enabled,canOutput; Guid id; double force;
                    lock(gate) {
                        double now=clock.Elapsed.TotalSeconds;
                        if(replaying && replay!=null) {
                            while(replayIndex<replay.Count && replay[replayIndex].At<=now-replayStart) { var frame=replay[replayIndex++]; feel.Push(frame.Sample,replayStart+frame.At,settings); vehicle="Replay: "+frame.Sample.Vehicle; }
                            if(replayIndex==replay.Count && now-replayStart>replay[replay.Count-1].At+.15) { armed=false; feel.Reset(); issue="Replay finished. Replay again to compare settings."; }
                        }
                        preview=feel.Force(now,settings); clipped=feel.Clipped; suspension=feel.Suspension; acceleration=feel.Acceleration;
                        enabled=armed && selected!=Guid.Empty; id=selected; canOutput=replaying ? allowReplay && focused : Program.GameFocused(); force=enabled && canOutput ? preview : 0;
                    }
                    try {
                        if(wheel!=null && opened!=id) { wheel.Dispose(); wheel=null; }
                        if(enabled && wheel==null) { wheel=new Wheel(); wheel.Open(id); opened=id; }
                        if(wheel!=null) { wheel.Write(force); if(!enabled) { wheel.Dispose(); wheel=null; } }
                        double sent=clock.Elapsed.TotalSeconds; longest=Math.Max(longest,sent-lastUpdate); lastUpdate=sent; updates++;
                        lock(gate) {
                            commanded=force; scopeFrames.Enqueue(new ScopeFrame(suspension/.45,acceleration/6,commanded/.1,preview/.1)); while(scopeFrames.Count>1000) scopeFrames.Dequeue();
                            if(sent-measuredStart>=1) { outputHz=updates/(sent-measuredStart); outputGapMs=longest*1000; measuredStart=sent; updates=0; longest=0; }
                        }
                    } catch(Exception e) { if(wheel!=null) { wheel.Dispose(); wheel=null; } lock(gate) { armed=false; commanded=0; issue="Output stopped: "+e.Message; } }
                    // Faster sampling supports the separate 50-90 Hz road carrier.
                    // The measured loop rate remains visible; Windows is not real-time.
                    Thread.Sleep(2);
                }
            } finally { if(wheel!=null) wheel.Dispose(); if(preciseTimer) timeEndPeriod(1); }
        }
        void Tick(object sender,EventArgs e) {
            if(testing) {
                if(testTicks==0) {
                    if(sliders[2].Visible || !sliders[0].Visible || !sliders[1].Visible) throw new Exception("Basic mode visibility failed");
                    if(wheels.Text!="no device detected" || selected!=Guid.Empty || arm.Enabled) throw new Exception("Empty device state failed");
                    SavePreview("dashboard-basic-preview.png"); advancedToggle.PerformClick();
                    if(!sliders[7].Visible) throw new Exception("Advanced mode did not reveal all sliders");
                }
                if(testTicks==5) { mode.SelectedIndex=1; if(!settings.Enhanced || settings.Original || !sliders[5].Enabled) throw new Exception("V2 selector failed"); }
                if(testTicks==15) { mode.SelectedIndex=2; if(!settings.Original || sliders[5].Enabled) throw new Exception("Original selector failed"); }
                if(testTicks==25) { mode.SelectedIndex=0; if(settings.Enhanced || settings.Original || !sliders[5].Enabled) throw new Exception("V1 revert selector failed"); }
                if(testTicks==30) { sliders[7].Value=85; if(settings.RoadFrequency!=85 || settings.TextureFrequency!=28) throw new Exception("Independent road pitch failed"); sliders[7].Value=75; }
            }
            lock(gate) {
                focused=ContainsFocus;
                if(testing) { testTicks++; suspension=Math.Sin(testTicks*.2)*.2; acceleration=Math.Sin(testTicks*.1)*2; commanded=Math.Sin(testTicks*.2)*.025; }
                if(testing) scope.Add(suspension/.45,acceleration/6,commanded/.1,preview/.1);
                while(scopeFrames.Count>0) { var frame=scopeFrames.Dequeue(); scope.Add(frame.Suspension,frame.Acceleration,frame.Force,frame.Preview); }
                scope.Invalidate();
                if(recorder!=null && recorder.Error!=null) { issue="Recording stopped: "+recorder.Error; recorder.Dispose(); recorder=null; }
                values.Text=string.Format("Suspension {0:+0.000;-0.000;0.000} m/s   •   Vertical {1:+0.00;-0.00;0.00} m/s²\nPreview {2:+0.00;-0.00;0.00}%   •   Commanded {3:+0.00;-0.00;0.00}%   {4}",suspension,acceleration,preview*100,commanded*100,clipped ? "LIMITING":"");
                arm.Text=armed ? "Disable output" : "Enable output"; record.Text=recorder==null ? "Record drive" : "Stop recording";
                status.Text=(replaying ? "REPLAY" : "LIVE")+" • "+vehicle+" • "+(armed ? "Armed" : "Output off")+(recorder!=null ? " • RECORDING" : "")+string.Format(" • Loop {0:0} Hz / longest {1:0.0} ms",outputHz,outputGapMs);
                surfaceStatus.Text=feel.SurfaceStatus+(settings.RoadTexture==0 ? " • Road buzz off":"");
                string nextMessage=issue;
                if(!testing && receiver!=null && issue=="") nextMessage="Packets "+receiver.Packets+" • Invalid "+receiver.Invalid+" • Connection errors "+receiver.ConnectionErrors+" • Gaps "+receiver.Gaps+" • Last gap "+receiver.LastGap.ToString("0.00")+"s"+(clock.Elapsed.TotalSeconds-receiver.LastPacketTime>.25 ? " • Waiting for telemetry" : "");
                // Publish once: clearing the auto-sized label first collapses its row
                // and moves Surface data up and down on every 25 ms refresh.
                if(message.Text!=nextMessage) message.Text=nextMessage;
            }
            if(testing && testTicks==45) {
                if(!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(Handle),new IntPtr(-4))) throw new Exception("Dashboard is not per-monitor V2 DPI aware");
                SavePreview("dashboard-preview.png");
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dashboard-ui-test.txt"),"PerMonitorV2 verified; window DPI: "+GetDpiForWindow(Handle)+". Comparison and independent pitch checks passed.");
                ClientSize=new Size((int)(760*GetDpiForWindow(Handle)/96f),(int)(580*GetDpiForWindow(Handle)/96f));
            }
            if(testing && testTicks==55) { var before=settings.Copy(); advancedToggle.PerformClick(); if(sliders[2].Visible || settings.RoadFrequency!=before.RoadFrequency || settings.Texture!=before.Texture) throw new Exception("Collapsing advanced changed tuning"); SavePreview("dashboard-compact-preview.png"); Close(); }
        }
    }
}
