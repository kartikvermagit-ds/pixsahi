using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace PixSahi {
    static class Tests {
        static readonly List<string> checks=new List<string>();
        static void Check(bool ok,string description){if(!ok)throw new Exception(description);checks.Add("PASS  "+description);}
        static void Fails(Action action,string description){bool failed=false;try{action();}catch(ArgumentException){failed=true;}catch(InvalidOperationException){failed=true;}Check(failed,description);}
        static string Hash(string path){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        static Bitmap Fixture(int w,int h,bool noise){
            var image=new Bitmap(w,h);var random=new Random(42);
            if(noise){for(int y=0;y<h;y++)for(int x=0;x<w;x++)image.SetPixel(x,y,Color.FromArgb(random.Next(256),random.Next(256),random.Next(256)));}
            else using(var g=Graphics.FromImage(image)){
                g.Clear(Color.White);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using(var p=new Pen(Color.FromArgb(39,57,96),Math.Max(2,w/120f))){
                    g.DrawBezier(p,new PointF(w*.12f,h*.65f),new PointF(w*.35f,-h*.4f),new PointF(w*.11f,h*1.35f),new PointF(w*.48f,h*.39f));
                    g.DrawBezier(p,new PointF(w*.40f,h*.60f),new PointF(w*.54f,h*.13f),new PointF(w*.42f,h*.93f),new PointF(w*.69f,h*.39f));
                    g.DrawBezier(p,new PointF(w*.64f,h*.62f),new PointF(w*.77f,h*.29f),new PointF(w*.72f,h*.85f),new PointF(w*.88f,h*.38f));
                    g.DrawLine(p,w*.22f,h*.82f,w*.9f,h*.68f);
                }
            }return image;
        }
        static void Screenshot(MainForm form,string path){Application.DoEvents();form.PerformLayout();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path,ImageFormat.Png);}}
        internal static void Run(string folder){
            Directory.CreateDirectory(folder);
            var spec=new ImageSpec{Width=200,Height=230,MaxKB=50};
            using(var noise=Fixture(600,450,true)){
                var jpeg=Imaging.Prepare(noise,spec);
                Check(jpeg.Bytes.Length<=50000,"JPG fits exact 50,000-byte limit");
                using(var decoded=Imaging.Decode(jpeg.Bytes))Check(decoded.Width==200&&decoded.Height==230,"JPEG keeps requested pixel dimensions");
                var tight=new ImageSpec{Width=200,Height=230,MaxKB=8};var compressed=Imaging.Prepare(noise,tight);
                Check(compressed.Bytes.Length<=8000&&compressed.Quality<95,"Compression adjusts quality for a tighter limit");
                Fails(delegate{Imaging.Prepare(noise,new ImageSpec{Width=600,Height=450,MaxKB=1});},"Impossible JPG limits produce an error instead of oversized output");
                Fails(delegate{Imaging.Prepare(noise,new ImageSpec{Width=600,Height=450,MaxKB=1,Png=true});},"Oversized PNG is rejected without changing dimensions");
                var png=Imaging.Prepare(noise,new ImageSpec{Width=100,Height=120,MaxKB=100,Png=true});
                using(var decoded=Imaging.Decode(png.Bytes))Check(decoded.Width==100&&decoded.Height==120,"PNG keeps exact dimensions and is decodable");
                Check(png.Bytes[0]==137&&png.Bytes[1]==80&&png.Bytes.Length<=100000,"PNG signature and maximum size");
                Fails(delegate{Imaging.Prepare(noise,new ImageSpec{Width=0});},"Invalid dimensions rejected");
                Fails(delegate{Imaging.Prepare(noise,new ImageSpec{Zoom=float.NaN});},"Invalid crop values rejected");
                File.WriteAllBytes(Path.Combine(folder,"compressed-photo.jpg"),compressed.Bytes);
            }
            using(var source=new Bitmap(600,200)){
                using(var g=Graphics.FromImage(source)){g.Clear(Color.Red);g.FillRectangle(Brushes.Blue,300,0,300,200);}
                var fit=new ImageSpec{Width=200,Height=200,MaxKB=100,Fit=true};
                using(var image=Imaging.Render(source,fit)){
                    Check(image.GetPixel(100,5).ToArgb()==Color.White.ToArgb(),"Fit mode adds white margins");
                    Check(image.GetPixel(20,100).R>200&&image.GetPixel(180,100).B>200,"Fit mode preserves both ends of the source");
                }
                var crop=new ImageSpec{Width=200,Height=200,Zoom=2,CenterX=0,CenterY=1};RectangleF r=Imaging.Crop(source.Size,crop);
                Check(r.X>=0&&r.Y>=0&&r.Right<=source.Width+.01f&&r.Bottom<=source.Height+.01f,"Crop stays inside the image at edge positions");
                Check(Math.Abs(r.Width/r.Height-1)<.001f,"Crop keeps target aspect ratio");
                using(var image=Imaging.Render(source,crop))Check(image.GetPixel(100,100).R>200,"Crop position changes the exported content");
            }
            using(var transparent=new Bitmap(100,100,PixelFormat.Format32bppArgb))using(var image=Imaging.Render(transparent,new ImageSpec{Width=100,Height=100}))Check(image.GetPixel(50,50).ToArgb()==Color.White.ToArgb(),"Transparency is flattened on white");
            using(var image=new Bitmap(3,2)){
                image.SetPixel(0,0,Color.Red);image.SetPixel(2,0,Color.Blue);image.SetPixel(0,1,Color.Green);image.SetPixel(2,1,Color.Yellow);
                Color[] topLeft={Color.Red,Color.Blue,Color.Yellow,Color.Green,Color.Red,Color.Green,Color.Yellow,Color.Blue};
                for(int n=1;n<=8;n++)using(var oriented=new Bitmap(image)){
                    Imaging.Orient(oriented,n);Check(oriented.GetPixel(0,0).ToArgb()==topLeft[n-1].ToArgb(),"EXIF orientation "+n+" maps corners correctly");
                    Check(oriented.Width==(n>=5?2:3)&&oriented.Height==(n>=5?3:2),"EXIF orientation "+n+" dimensions");
                }
            }
            string signaturePath=Path.Combine(folder,"sample-signature.png");using(var signature=Fixture(900,300,false))signature.Save(signaturePath,ImageFormat.Png);
            string originalHash=Hash(signaturePath);
            using(var source=Imaging.Load(signaturePath)){
                string renamed=Path.Combine(folder,"renamed.png");File.Move(signaturePath,renamed);File.Move(renamed,signaturePath);Check(true,"Loading releases the original file handle");
                var result=Imaging.Prepare(source,new ImageSpec{Width=300,Height=100,MaxKB=20,Fit=true});
                string target=Path.Combine(folder,"sample-signature-ready.jpg");Imaging.Save(result,target,signaturePath);
                Check(Hash(signaturePath)==originalHash,"Preparing and saving leaves original bytes unchanged");
                Check(File.ReadAllBytes(target).SequenceEqual(result.Bytes),"Saved bytes match the prepared result");
                Imaging.Save(result,target,signaturePath);Check(File.ReadAllBytes(target).SequenceEqual(result.Bytes),"Replacing an existing output works");
                Fails(delegate{Imaging.Save(result,signaturePath,signaturePath);},"Saving over the original is blocked");
                Fails(delegate{Imaging.Save(result,Path.Combine(folder,"wrong.png"),signaturePath);},"Mismatched output extension is blocked");
                using(var stream=new MemoryStream(result.Bytes))using(var image=Image.FromStream(stream))Check(Array.IndexOf(image.PropertyIdList,0x112)<0,"Output has no EXIF orientation tag");
            }
            byte[] rawJpeg;using(var b=Fixture(60,30,false))rawJpeg=Imaging.Encode(b,false,95);
            byte[] exif={0xFF,0xE1,0,34,69,120,105,102,0,0,73,73,42,0,8,0,0,0,1,0,18,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0};
            string rotatedPath=Path.Combine(folder,"phone-rotated.jpg");using(var file=File.Create(rotatedPath)){file.Write(rawJpeg,0,2);file.Write(exif,0,exif.Length);file.Write(rawJpeg,2,rawJpeg.Length-2);}
            using(var phone=Imaging.Load(rotatedPath))Check(phone.Width==30&&phone.Height==60,"Loader honors orientation metadata in a real JPEG fixture");
            string corrupt=Path.Combine(folder,"broken.jpg");File.WriteAllText(corrupt,"not an image");Fails(delegate{using(var b=Imaging.Load(corrupt)){}},"Corrupt images rejected");
            Fails(delegate{using(var b=Imaging.Load(Path.Combine(folder,"unsupported.heic"))){}},"Unsupported formats rejected");
            using(var form=new MainForm()){
                form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();
                Screenshot(form,Path.Combine(folder,"empty-window.png"));
                Check(form.OpenImage(signaturePath),"App loads an input image");form.Preset(true);
                var task=form.PrepareAsync();while(!task.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}task.GetAwaiter().GetResult();
                Check(form.HasResult,"Prepare button flow enables saving a valid result");Screenshot(form,Path.Combine(folder,"preview-window.png"));
                form.width.Value=310;Check(!form.HasResult,"Changing dimensions invalidates stale prepared output");
                var stale=form.PrepareAsync();form.kb.Value=21;while(!stale.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}stale.GetAwaiter().GetResult();
                Check(!form.HasResult,"An in-flight result is discarded after settings change");
                form.Size=form.MinimumSize;Screenshot(form,Path.Combine(folder,"compact-window.png"));
                Check(form.Controls[0].Width==form.ClientSize.Width,"Layout fills the resized window");
                form.Preset(false);
                var second=form.PrepareAsync();while(!second.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}second.GetAwaiter().GetResult();
                float before=form.input.Spec.CenterX;RectangleF viewport=form.input.Viewport;
                var down=typeof(Control).GetMethod("OnMouseDown",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var move=typeof(Control).GetMethod("OnMouseMove",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var up=typeof(Control).GetMethod("OnMouseUp",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                int mx=(int)(viewport.X+viewport.Width/2),my=(int)(viewport.Y+viewport.Height/2);
                down.Invoke(form.input,new object[]{new MouseEventArgs(MouseButtons.Left,1,mx,my,0)});
                move.Invoke(form.input,new object[]{new MouseEventArgs(MouseButtons.Left,0,mx+25,my,0)});
                up.Invoke(form.input,new object[]{new MouseEventArgs(MouseButtons.Left,1,mx+25,my,0)});
                Check(form.input.Spec.CenterX<before,"Dragging the preview updates crop position");
                Check(!form.HasResult,"Dragging invalidates the old prepared image");
                form.Close();
            }
            checks.Add("Manual checks still needed: Windows file dialogs, real-photo crop suitability, mixed-DPI displays, and portal acceptance against the portal's current rules.");
            File.WriteAllLines(Path.Combine(folder,"PASSED.txt"),checks.ToArray());
        }
    }
}
