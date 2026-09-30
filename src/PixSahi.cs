using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("PixSahi")]
[assembly: AssemblyDescription("Offline photo and signature preparation for online forms")]
[assembly: AssemblyProduct("PixSahi")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace PixSahi {
    static class Style {
        public static Color Paper = Color.FromArgb(246, 247, 242), Ink = Color.FromArgb(31, 45, 39), Muted = Color.FromArgb(106, 116, 110), Green = Color.FromArgb(36, 99, 75), Line = Color.FromArgb(223, 229, 221);
        public static Label Label(string text, float size, bool bold) { return new Label {Text = text, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Ink, AutoSize = false}; }
        public static Button Button(string text, bool primary) {
            var b = new Button {Text=text, Height=42, FlatStyle=FlatStyle.Flat, Cursor=Cursors.Hand, BackColor=primary?Green:Color.White, ForeColor=primary?Color.White:Ink, Font=new Font("Segoe UI",10,FontStyle.Bold), UseVisualStyleBackColor=false};
            b.FlatAppearance.BorderColor=Line; b.FlatAppearance.BorderSize=primary?0:1;
            b.EnabledChanged+=delegate{b.BackColor=b.Enabled?(primary?Green:Color.White):Color.FromArgb(225,230,222);};return b;
        }
    }
    sealed class ImageCanvas : Control {
        internal Bitmap Picture;
        internal ImageSpec Spec = new ImageSpec();
        internal bool Editable;
        internal string EmptyTitle = "Your image goes here", EmptyHint = "Drop a JPG, PNG, or BMP to start";
        internal event EventHandler CropChanged;
        bool dragging; Point origin; float centerX, centerY; RectangleF startCrop;
        internal ImageCanvas() { DoubleBuffered=true; BackColor=Color.FromArgb(239,242,235); SetStyle(ControlStyles.ResizeRedraw,true); }
        internal RectangleF Viewport {
            get { return Imaging.Contain(new Size(Spec.Width,Spec.Height),new RectangleF(22,22,Math.Max(1,Width-44),Math.Max(1,Height-44))); }
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
            if(Picture==null) {
                if(Height<180){using(var f=new Font("Segoe UI",9,FontStyle.Bold))using(var b=new SolidBrush(Style.Muted))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(EmptyTitle,f,b,ClientRectangle,sf);return;}
                using(var pen=new Pen(Color.FromArgb(170,186,169),2)) {
                    var r=new Rectangle(Width/2-23,Height/2-57,46,38);g.DrawRectangle(pen,r);
                    g.DrawLines(pen,new Point[]{new Point(r.Left+6,r.Bottom-7),new Point(r.Left+18,r.Top+19),new Point(r.Left+26,r.Top+27),new Point(r.Right-6,r.Top+13)});
                    g.DrawEllipse(pen,r.Left+8,r.Top+7,5,5);
                }
                using(var f=new Font("Segoe UI",11,FontStyle.Bold))using(var b=new SolidBrush(Style.Ink))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    g.DrawString(EmptyTitle,f,b,new RectangleF(8,Height/2,Width-16,30),sf);
                using(var f=new Font("Segoe UI",9))using(var b=new SolidBrush(Style.Muted))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    g.DrawString(EmptyHint,f,b,new RectangleF(15,Height/2+34,Width-30,48),sf);
                return;
            }
            RectangleF view=Editable?Viewport:Imaging.Contain(Picture.Size,new RectangleF(22,22,Math.Max(1,Width-44),Math.Max(1,Height-44)));
            using(var shadow=new SolidBrush(Color.FromArgb(20,0,0,0)))g.FillRectangle(shadow,view.X+3,view.Y+5,view.Width,view.Height);
            g.FillRectangle(Brushes.White,view);
            var state=g.Save();g.SetClip(view);
            if(Editable) Imaging.Draw(g,Picture,view,Spec);
            else {g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(Picture,view);}
            g.Restore(state);
            using(var border=new Pen(Style.Line,1))g.DrawRectangle(border,view.X,view.Y,view.Width,view.Height);
            if(Editable && !Spec.Fit) {
                using(var pen=new Pen(Color.FromArgb(155,255,255,255),1)) {
                    for(int n=1;n<3;n++) {g.DrawLine(pen,view.X+view.Width*n/3,view.Y,view.X+view.Width*n/3,view.Bottom);g.DrawLine(pen,view.X,view.Y+view.Height*n/3,view.Right,view.Y+view.Height*n/3);}
                }
                using(var pen=new Pen(Style.Green,3)) {
                    float l=Math.Min(16,Math.Min(view.Width,view.Height)/5);
                    g.DrawLines(pen,new PointF[]{new PointF(view.X,view.Y+l),new PointF(view.X,view.Y),new PointF(view.X+l,view.Y)});
                    g.DrawLines(pen,new PointF[]{new PointF(view.Right-l,view.Bottom),new PointF(view.Right,view.Bottom),new PointF(view.Right,view.Bottom-l)});
                }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);if(!Editable||Picture==null||Spec.Fit||e.Button!=MouseButtons.Left||!Viewport.Contains(e.Location))return;
            dragging=true;Capture=true;origin=e.Location;startCrop=Imaging.Crop(Picture.Size,Spec);
            centerX=(startCrop.X+startCrop.Width/2)/Picture.Width;centerY=(startCrop.Y+startCrop.Height/2)/Picture.Height;Cursor=Cursors.SizeAll;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);if(!dragging)return;
            float halfX=startCrop.Width/(2*Picture.Width),halfY=startCrop.Height/(2*Picture.Height);
            Spec.CenterX=Math.Max(halfX,Math.Min(1-halfX,centerX-(e.X-origin.X)/Viewport.Width*startCrop.Width/Picture.Width));
            Spec.CenterY=Math.Max(halfY,Math.Min(1-halfY,centerY-(e.Y-origin.Y)/Viewport.Height*startCrop.Height/Picture.Height));
            Invalidate();if(CropChanged!=null)CropChanged(this,EventArgs.Empty);
        }
        protected override void OnMouseUp(MouseEventArgs e) {base.OnMouseUp(e);dragging=false;Capture=false;Cursor=Cursors.Default;}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Cursor=Cursors.Default;}}
    }
    sealed class MainForm : Form {
        internal readonly ImageCanvas input=new ImageCanvas{Editable=true}, output=new ImageCanvas{EmptyTitle="Ready when you are",EmptyHint="Set your requirements, then click Prepare image"};
        internal readonly NumericUpDown width=Number(200,16,4000), height=Number(230,16,4000), kb=Number(50,1,20000);
        readonly ComboBox format=new ComboBox(), sizing=new ComboBox();
        readonly TrackBar zoom=new TrackBar{Minimum=100,Maximum=300,Value=100,TickStyle=TickStyle.None,AutoSize=false,Height=32};
        readonly Label zoomLabel=Style.Label("Zoom  100%",9,false), sourceInfo=Style.Label("No image selected",9,false), outputInfo=Style.Label("No export prepared",9,false), status=Style.Label("Add a photo or signature to get started.",10,false);
        readonly Button prepare=Style.Button("Prepare image",true), save=Style.Button("Save image...",true), photo=Style.Button("Photo",false), signature=Style.Button("Signature",false);
        Bitmap original, prepared; string sourcePath; ImageResult result;
        bool updating, busy; int revision;
        readonly ToolTip tips=new ToolTip();
        internal MainForm() {
            SuspendLayout();Text="PixSahi  |  Offline photo & signature tool";ClientSize=new Size(1120,780);MinimumSize=new Size(940,640);
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;
            Font=new Font("Segoe UI",10);BackColor=Style.Paper;ForeColor=Style.Ink;AllowDrop=true;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28,20,28,20),ColumnCount=1,RowCount=3};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,103));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,84));Controls.Add(root);
            var header=new Panel{Dock=DockStyle.Fill};
            var brand=Style.Label("PixSahi",24,true);brand.SetBounds(0,0,220,45);header.Controls.Add(brand);
            var subtitle=Style.Label("Make your photo fit.",12,false);subtitle.SetBounds(1,48,350,27);header.Controls.Add(subtitle);
            var privacy=Style.Label("OFFLINE  /  NO UPLOADS",9,true);privacy.TextAlign=ContentAlignment.MiddleRight;privacy.Anchor=AnchorStyles.Top|AnchorStyles.Right;privacy.SetBounds(720,13,330,26);header.Controls.Add(privacy);
            header.Resize+=delegate{privacy.Left=Math.Max(350,header.Width-privacy.Width);};root.Controls.Add(header,0,0);
            var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,286));main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.Controls.Add(main,0,1);
            var settings=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Color.White,Margin=new Padding(0,0,16,0)};
            var settingsBody=new Panel{Width=230,Height=510,Location=new Point(12,12)};settings.Controls.Add(settingsBody);main.Controls.Add(settings,0,0);
            var kind=Style.Label("01   CHOOSE A STARTING POINT",8,true);kind.SetBounds(0,0,230,24);settingsBody.Controls.Add(kind);
            photo.SetBounds(0,30,106,37);signature.SetBounds(114,30,110,37);settingsBody.Controls.AddRange(new Control[]{photo,signature});
            var note=Style.Label("Examples only. Use the dimensions and limit requested by your form.",8.5f,false);note.ForeColor=Style.Muted;note.SetBounds(0,76,228,42);settingsBody.Controls.Add(note);
            var pixels=Style.Label("02   OUTPUT DIMENSIONS",8,true);pixels.SetBounds(0,120,230,24);settingsBody.Controls.Add(pixels);
            AddLabel(settingsBody,"Width (px)",0,148,100);AddLabel(settingsBody,"Height (px)",119,148,105);
            width.SetBounds(0,172,105,30);height.SetBounds(119,172,105,30);settingsBody.Controls.AddRange(new Control[]{width,height});
            AddLabel(settingsBody,"Maximum file size (KB)",0,214,220);kb.SetBounds(0,238,105,30);settingsBody.Controls.Add(kb);
            var unit=Style.Label("1 KB = 1,000 bytes",8,false);unit.ForeColor=Style.Muted;unit.SetBounds(116,242,115,25);settingsBody.Controls.Add(unit);
            AddLabel(settingsBody,"File format",0,277,224);format.DropDownStyle=ComboBoxStyle.DropDownList;format.Items.AddRange(new object[]{"JPG - smaller files","PNG - lossless"});format.SelectedIndex=0;format.SetBounds(0,301,224,30);settingsBody.Controls.Add(format);
            AddLabel(settingsBody,"Image fit",0,339,224);sizing.DropDownStyle=ComboBoxStyle.DropDownList;sizing.Items.AddRange(new object[]{"Crop to fill","Fit entire image (white margins)"});sizing.SelectedIndex=0;sizing.SetBounds(0,363,224,30);settingsBody.Controls.Add(sizing);
            zoomLabel.SetBounds(0,407,150,24);zoom.SetBounds(0,432,224,30);settingsBody.Controls.AddRange(new Control[]{zoomLabel,zoom});
            var reset=Style.Button("Reset crop",false);reset.SetBounds(0,474,106,36);var rotate=Style.Button("Rotate 90",false);rotate.SetBounds(114,474,110,36);settingsBody.Controls.AddRange(new Control[]{reset,rotate});
            var previewArea=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};previewArea.RowStyles.Add(new RowStyle(SizeType.Absolute,52));previewArea.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.Controls.Add(previewArea,1,0);
            var toolbar=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};var choose=Style.Button("+  Add image",false);choose.SetBounds(0,0,150,40);toolbar.Controls.Add(choose);
            var supported=Style.Label("JPG, PNG, BMP  /  up to 40 MB",9,false);supported.ForeColor=Style.Muted;supported.SetBounds(165,11,290,25);toolbar.Controls.Add(supported);previewArea.Controls.Add(toolbar,0,0);
            var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));previewArea.Controls.Add(cards,0,1);
            cards.Controls.Add(Card("YOUR FRAME","Drag to adjust crop",input,sourceInfo),0,0);
            cards.Controls.Add(Card("FINAL IMAGE","Preview of the saved file",output,outputInfo),1,0);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,15,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,166));root.Controls.Add(footer,0,2);
            status.Dock=DockStyle.Fill;status.Padding=new Padding(0,3,16,0);footer.Controls.Add(status,0,0);
            prepare.Dock=DockStyle.Top;prepare.Margin=new Padding(0,3,10,0);save.Dock=DockStyle.Top;save.Margin=new Padding(0,3,0,0);save.Enabled=false;prepare.Enabled=false;footer.Controls.Add(prepare,1,0);footer.Controls.Add(save,2,0);
            choose.Click+=delegate{Choose();};photo.Click+=delegate{Preset(false);};signature.Click+=delegate{Preset(true);};
            width.ValueChanged+=Changed;height.ValueChanged+=Changed;kb.ValueChanged+=Changed;format.SelectedIndexChanged+=Changed;sizing.SelectedIndexChanged+=Changed;zoom.ValueChanged+=Changed;
            reset.Click+=delegate{updating=true;zoom.Value=100;input.Spec.CenterX=.5f;input.Spec.CenterY=.5f;updating=false;Changed(this,EventArgs.Empty);};
            rotate.Click+=delegate{if(original==null)return;original.RotateFlip(RotateFlipType.Rotate90FlipNone);input.Spec.CenterX=.5f;input.Spec.CenterY=.5f;zoom.Value=100;UpdateSourceInfo();Changed(this,EventArgs.Empty);};
            input.CropChanged+=delegate{InvalidateResult();};input.DoubleClick+=delegate{if(original==null)Choose();};
            prepare.Click+=async delegate{await PrepareAsync();};save.Click+=delegate{SaveOutput();};
            DragEnter+=DropEnter;DragDrop+=DropImage;input.AllowDrop=true;input.DragEnter+=DropEnter;input.DragDrop+=DropImage;
            tips.SetToolTip(format,"PNG keeps pixels lossless but can be larger. JPG adjusts quality to meet your limit.");
            tips.SetToolTip(sizing,"Crop fills the requested dimensions. Fit keeps the entire image with white margins.");
            tips.SetToolTip(output,"The preview is decoded from the exact bytes that Save image writes.");
            KeyPreview=true;KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.O){Choose();e.SuppressKeyPress=true;}};
            photo.BackColor=Color.FromArgb(229,237,220);ApplySpec();ResumeLayout(true);
        }
        static NumericUpDown Number(int value,int min,int max){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,ThousandsSeparator=false,Font=new Font("Segoe UI",11),BorderStyle=BorderStyle.FixedSingle};}
        static void AddLabel(Control panel,string text,int x,int y,int width){var label=Style.Label(text,9,false);label.SetBounds(x,y,width,23);panel.Controls.Add(label);}
        static Control Card(string title,string subtitle,ImageCanvas canvas,Label info){
            var card=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(14,15,14,10),Margin=new Padding(0,0,10,0),ColumnCount=1,RowCount=4};
            card.RowStyles.Add(new RowStyle(SizeType.Absolute,23));card.RowStyles.Add(new RowStyle(SizeType.Absolute,30));card.RowStyles.Add(new RowStyle(SizeType.Percent,100));card.RowStyles.Add(new RowStyle(SizeType.Absolute,57));
            var titleLabel=Style.Label(title,9,true);titleLabel.Dock=DockStyle.Fill;card.Controls.Add(titleLabel,0,0);
            var sub=Style.Label(subtitle,8.5f,false);sub.ForeColor=Style.Muted;sub.Dock=DockStyle.Fill;card.Controls.Add(sub,0,1);
            canvas.Dock=DockStyle.Fill;canvas.Margin=Padding.Empty;card.Controls.Add(canvas,0,2);info.Dock=DockStyle.Fill;info.Padding=new Padding(0,12,0,0);card.Controls.Add(info,0,3);return card;
        }
        internal void Preset(bool sign){updating=true;width.Value=sign?300:200;height.Value=sign?100:230;kb.Value=sign?20:50;format.SelectedIndex=0;sizing.SelectedIndex=sign?1:0;zoom.Value=100;input.Spec.CenterX=.5f;input.Spec.CenterY=.5f;photo.BackColor=sign?Color.White:Color.FromArgb(229,237,220);signature.BackColor=sign?Color.FromArgb(229,237,220):Color.White;updating=false;Changed(this,EventArgs.Empty);}
        ImageSpec GetSpec(){return new ImageSpec{Width=(int)width.Value,Height=(int)height.Value,MaxKB=(int)kb.Value,Png=format.SelectedIndex==1,Fit=sizing.SelectedIndex==1,Zoom=zoom.Value/100f,CenterX=input.Spec.CenterX,CenterY=input.Spec.CenterY};}
        void ApplySpec(){input.Spec=GetSpec();input.Invalidate();zoom.Enabled=!input.Spec.Fit;zoomLabel.Text="Zoom  "+zoom.Value+"%";}
        void Changed(object sender,EventArgs e){if(updating)return;ApplySpec();InvalidateResult();}
        void InvalidateResult(){revision++;result=null;save.Enabled=false;output.Picture=null;if(prepared!=null){prepared.Dispose();prepared=null;}output.Invalidate();outputInfo.Text="Prepare to check dimensions and size";if(original!=null){status.ForeColor=Style.Muted;status.Text=busy?"Settings changed. Prepare again after the current operation finishes.":"Frame your image, then prepare it to check the final file.";}}
        void Choose(){using(var dialog=new OpenFileDialog{Title="Choose a photo or signature",Filter="Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK)OpenImage(dialog.FileName);}
        internal bool OpenImage(string path){
            try{var image=Imaging.Load(path);input.Picture=null;if(original!=null)original.Dispose();original=image;sourcePath=Path.GetFullPath(path);input.Picture=original;updating=true;zoom.Value=100;input.Spec.CenterX=.5f;input.Spec.CenterY=.5f;updating=false;ApplySpec();UpdateSourceInfo();InvalidateResult();prepare.Enabled=!busy;return true;}
            catch(Exception ex){ShowError("Could not open image. "+ex.Message);return false;}
        }
        void UpdateSourceInfo(){sourceInfo.Text=Path.GetFileName(sourcePath)+Environment.NewLine+original.Width+" x "+original.Height+" px";}
        void DropEnter(object sender,DragEventArgs e){e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;}
        void DropImage(object sender,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files==null||files.Length==0)return;if(files.Length!=1){ShowError("Please add one image at a time.");return;}OpenImage(files[0]);}
        internal async Task PrepareAsync(){
            if(original==null||busy)return;ImageSpec spec=GetSpec();int version=revision;Bitmap snapshot;
            try{spec.Validate();snapshot=new Bitmap(original);}catch(Exception ex){ShowError(ex.Message);return;}
            busy=true;prepare.Enabled=false;save.Enabled=false;prepare.Text="Preparing...";status.ForeColor=Style.Muted;status.Text="Resizing and checking the file-size limit...";
            try {
                ImageResult next=await Task.Run(delegate{using(snapshot)return Imaging.Prepare(snapshot,spec);});
                if(IsDisposed||Disposing)return;if(version!=revision){status.Text="Your settings changed. Click Prepare image again.";return;}
                SetResult(next);
            }catch(Exception ex){if(!IsDisposed&&!Disposing&&version==revision){InvalidateResult();ShowError(ex.Message);}}
            finally{if(!IsDisposed&&!Disposing){busy=false;prepare.Text="Prepare image";prepare.Enabled=original!=null;}}
        }
        internal void SetResult(ImageResult next){
            Bitmap decoded=Imaging.Decode(next.Bytes);output.Picture=null;if(prepared!=null)prepared.Dispose();prepared=decoded;result=next;output.Picture=prepared;output.Invalidate();save.Enabled=true;
            outputInfo.Text=next.Spec.Width+" x "+next.Spec.Height+" px  /  "+(next.Spec.Png?"PNG":"JPG")+Environment.NewLine+(next.Bytes.Length/1000.0).ToString("0.00")+" KB  ("+next.Bytes.Length.ToString("N0")+" bytes)";
            status.ForeColor=Style.Green;status.Text="Ready. Under "+next.Spec.MaxKB+" KB, at your exact dimensions.";
            if(!next.Spec.Png&&next.Quality<45)status.Text+=" Strong JPG compression: check the preview for readability.";
            RectangleF crop=next.Spec.Fit?new RectangleF(0,0,original.Width,original.Height):Imaging.Crop(original.Size,next.Spec);
            if(!next.Spec.Fit&&(crop.Width<next.Spec.Width||crop.Height<next.Spec.Height))status.Text+=" Enlarged from the source; detail may look soft.";
        }
        void SaveOutput(){
            if(result==null)return;var current=result;
            using(var dialog=new SaveFileDialog{Title="Save prepared image",Filter=current.Spec.Png?"PNG image (*.png)|*.png":"JPG image (*.jpg)|*.jpg",DefaultExt=current.Spec.Png?"png":"jpg",AddExtension=true,OverwritePrompt=true,FileName=Path.GetFileNameWithoutExtension(sourcePath)+"-pixsahi-"+current.Spec.Width+"x"+current.Spec.Height+(current.Spec.Png?".png":".jpg")}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{Imaging.Save(current,dialog.FileName,sourcePath);status.ForeColor=Style.Green;status.Text="Saved "+Path.GetFileName(dialog.FileName)+". Your original is unchanged.";}catch(Exception ex){ShowError("Could not save. "+ex.Message);}
            }
        }
        void ShowError(string message){status.ForeColor=Color.FromArgb(155,56,37);status.Text=message;tips.SetToolTip(status,message);}
        protected override void OnShown(EventArgs e){base.OnShown(e);if(StartPosition!=FormStartPosition.Manual){Rectangle area=Screen.FromControl(this).WorkingArea;if(Width>area.Width-20||Height>area.Height-20){MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width-20),Math.Min(MinimumSize.Height,area.Height-20));Size=new Size(Math.Min(Width,area.Width-20),Math.Min(Height,area.Height-20));Location=new Point(area.X+(area.Width-Width)/2,area.Y+(area.Height-Height)/2);}}}
        protected override void Dispose(bool disposing){if(disposing){revision++;input.Picture=null;output.Picture=null;if(original!=null)original.Dispose();if(prepared!=null)prepared.Dispose();tips.Dispose();}base.Dispose(disposing);}
        internal bool HasResult {get{return result!=null&&save.Enabled;}}
    }
    static class Program {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static int Main(string[] args){
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try {
                if(args.Length==2&&args[0]=="--self-test"){Tests.Run(args[1]);return 0;}
                using(var form=new MainForm()){if(args.Length==1)form.OpenImage(args[0]);Application.Run(form);}return 0;
            }catch(Exception ex){
                if(args.Length==2&&args[0]=="--self-test"){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"FAILED.txt"),ex.ToString());}
                else MessageBox.Show(ex.Message,"PixSahi",MessageBoxButtons.OK,MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
