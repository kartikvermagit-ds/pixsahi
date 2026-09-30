using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace PixSahi {
    public sealed class ImageSpec {
        public int Width = 200, Height = 230, MaxKB = 50;
        public bool Png, Fit;
        public float Zoom = 1, CenterX = .5f, CenterY = .5f;
        public void Validate() {
            if (Width < 16 || Height < 16 || Width > 4000 || Height > 4000)
                throw new ArgumentException("Use dimensions between 16 and 4,000 pixels.");
            if (MaxKB < 1 || MaxKB > 20000) throw new ArgumentException("Use a file-size limit between 1 and 20,000 KB.");
            if (float.IsNaN(Zoom) || Zoom < 1 || Zoom > 3 || float.IsNaN(CenterX) || float.IsNaN(CenterY)
                || CenterX < 0 || CenterX > 1 || CenterY < 0 || CenterY > 1)
                throw new ArgumentException("The crop position is invalid. Reset the crop and try again.");
        }
    }
    public sealed class ImageResult {
        public byte[] Bytes;
        public ImageSpec Spec;
        public int Quality;
    }
    public static class Imaging {
        const long MaxInputBytes = 40L * 1000 * 1000;
        public static Bitmap Load(string path) {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp")
                throw new ArgumentException("Choose a JPG, PNG, or BMP image. Convert HEIC or WebP to JPG/PNG first.");
            var info = new FileInfo(path);
            if (!info.Exists) throw new FileNotFoundException("That image could not be found.");
            if (info.Length > MaxInputBytes) throw new ArgumentException("Choose an image smaller than 40 MB.");
            try {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var image = Image.FromStream(stream, true, true)) {
                    if ((long)image.Width * image.Height > 40000000 || image.Width > 20000 || image.Height > 20000)
                        throw new ArgumentException("Choose an image up to 40 megapixels and 20,000 pixels per side.");
                    if (image.RawFormat.Guid != ImageFormat.Jpeg.Guid && image.RawFormat.Guid != ImageFormat.Png.Guid && image.RawFormat.Guid != ImageFormat.Bmp.Guid)
                        throw new ArgumentException("This file is not a supported JPG, PNG, or BMP image.");
                    if (Array.IndexOf(image.PropertyIdList, 0x112) >= 0) {
                        byte[] value = image.GetPropertyItem(0x112).Value;
                        if (value.Length >= 2) Orient(image, BitConverter.ToUInt16(value, 0));
                    }
                    var copy = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(copy)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImageUnscaled(image, 0, 0); }
                    return copy;
                }
            } catch (OutOfMemoryException) {
                throw new ArgumentException("The image is too large or is not a valid supported image.");
            }
        }
        internal static void Orient(Image image, int value) {
            switch (value) {
                case 2: image.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                case 4: image.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                case 5: image.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                case 7: image.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
            }
        }
        public static RectangleF Crop(Size source, ImageSpec spec) {
            float aspect = (float)spec.Width / spec.Height;
            float width = Math.Min(source.Width, source.Height * aspect) / spec.Zoom;
            float height = width / aspect;
            float x = Math.Max(0, Math.Min(source.Width - width, spec.CenterX * source.Width - width / 2));
            float y = Math.Max(0, Math.Min(source.Height - height, spec.CenterY * source.Height - height / 2));
            return new RectangleF(x, y, width, height);
        }
        public static RectangleF Contain(Size source, RectangleF bounds) {
            float scale = Math.Min(bounds.Width / source.Width, bounds.Height / source.Height);
            float w = source.Width * scale, h = source.Height * scale;
            return new RectangleF(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h);
        }
        public static void Draw(Graphics g, Image source, RectangleF destination, ImageSpec spec) {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            RectangleF crop = spec.Fit ? new RectangleF(0, 0, source.Width, source.Height) : Crop(source.Size, spec);
            RectangleF dest = spec.Fit ? Contain(source.Size, destination) : destination;
            using (var attributes = new ImageAttributes()) {
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source, new PointF[] {new PointF(dest.Left, dest.Top), new PointF(dest.Right, dest.Top), new PointF(dest.Left, dest.Bottom)}, crop, GraphicsUnit.Pixel, attributes);
            }
        }
        public static Bitmap Render(Bitmap source, ImageSpec spec) {
            spec.Validate();
            var output = new Bitmap(spec.Width, spec.Height, PixelFormat.Format24bppRgb);
            output.SetResolution(96, 96);
            try {
                using (var g = Graphics.FromImage(output)) { g.Clear(Color.White); Draw(g, source, new RectangleF(0, 0, spec.Width, spec.Height), spec); }
                return output;
            } catch { output.Dispose(); throw; }
        }
        internal static byte[] Encode(Bitmap image, bool png, int quality) {
            using (var stream = new MemoryStream()) {
                if (png) image.Save(stream, ImageFormat.Png);
                else {
                    var codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (var parameters = new EncoderParameters(1)) {
                        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                        image.Save(stream, codec, parameters);
                    }
                }
                return stream.ToArray();
            }
        }
        public static ImageResult Prepare(Bitmap source, ImageSpec spec) {
            spec.Validate();
            using (var output = Render(source, spec)) {
                long limit = (long)spec.MaxKB * 1000;
                byte[] bytes = Encode(output, spec.Png, 95);
                if (bytes.Length <= limit) return new ImageResult {Bytes = bytes, Spec = spec, Quality = spec.Png ? 100 : 95};
                if (spec.Png) throw new InvalidOperationException("PNG is " + (bytes.Length / 1000.0).ToString("0.0") + " KB at these dimensions. Choose JPG, increase the KB limit, or reduce the dimensions.");
                byte[] smallest = Encode(output, false, 10);
                if (smallest.Length > limit) throw new InvalidOperationException("This image cannot fit within " + spec.MaxKB + " KB at " + spec.Width + " x " + spec.Height + " px, even at low JPG quality. Increase the KB limit or reduce the dimensions.");
                int low = 10, high = 94, bestQuality = 10; byte[] best = smallest;
                while (low <= high) {
                    int quality = (low + high) / 2;
                    byte[] candidate = Encode(output, false, quality);
                    if (candidate.Length <= limit) { best = candidate; bestQuality = quality; low = quality + 1; }
                    else high = quality - 1;
                }
                return new ImageResult {Bytes = best, Spec = spec, Quality = bestQuality};
            }
        }
        public static Bitmap Decode(byte[] bytes) {
            using (var stream = new MemoryStream(bytes)) using (var image = Image.FromStream(stream)) return new Bitmap(image);
        }
        public static void Save(ImageResult result, string path, string sourcePath) {
            string full = Path.GetFullPath(path);
            if (!string.IsNullOrEmpty(sourcePath) && string.Equals(full, Path.GetFullPath(sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a different name. PixSahi keeps your original image unchanged.");
            string ext = Path.GetExtension(full).ToLowerInvariant();
            if (result.Spec.Png ? ext != ".png" : (ext != ".jpg" && ext != ".jpeg"))
                throw new InvalidOperationException("Use the " + (result.Spec.Png ? ".png" : ".jpg") + " extension for this output format.");
            if (result.Bytes.Length > (long)result.Spec.MaxKB * 1000) throw new InvalidOperationException("The output exceeds the selected size limit.");
            string temp = Path.Combine(Path.GetDirectoryName(full), ".pixsahi-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(result.Bytes, 0, result.Bytes.Length);
                if (File.Exists(full)) File.Replace(temp, full, null); else File.Move(temp, full);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
