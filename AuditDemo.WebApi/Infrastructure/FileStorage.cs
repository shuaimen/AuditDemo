using System;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Web;

namespace AuditDemo.WebApi.Infrastructure
{
    public static class FileStorage
    {
        private static string UploadRoot => ConfigurationManager.AppSettings["UploadRoot"] ?? "App_Data\\Uploads";
        private static int ImageMaxWidth
        {
            get
            {
                int w;
                return int.TryParse(ConfigurationManager.AppSettings["ImageMaxWidth"], out w) ? w : 1920;
            }
        }

        public static SavedFile SavePhoto(HttpPostedFile file, string subDir)
        {
            if (file == null || file.ContentLength <= 0) throw new ArgumentException("file is empty");
            if (file.ContentLength > 10 * 1024 * 1024) throw new ArgumentException("file exceeds 10MB");

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            // For .NET Framework + System.Drawing, HEIC is not supported out-of-the-box.
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".bmp" && ext != ".gif")
                throw new ArgumentException("only image files allowed");

            var baseDir = HttpContext.Current.Server.MapPath("~\\" + UploadRoot);
            if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

            var relDir = Path.Combine(UploadRoot, subDir ?? string.Empty);
            var absDir = HttpContext.Current.Server.MapPath("~\\" + relDir);
            Directory.CreateDirectory(absDir);

            var fileName = Guid.NewGuid().ToString("N") + ".jpg";
            var absPath = Path.Combine(absDir, fileName);

            using (var srcStream = file.InputStream)
            using (var img = Image.FromStream(srcStream))
            {
                SaveResizedJpeg(img, absPath, ImageMaxWidth);
            }

            var relPath = Path.Combine(relDir, fileName).Replace("\\", "/");
            var size = new FileInfo(absPath).Length;
            return new SavedFile { RelativePath = relPath, FileName = fileName, SizeBytes = size };
        }


        // Certificate module: allow PDF + images
        public static SavedFile SaveAttachment(HttpPostedFile file, string subDir)
        {
            if (file == null || file.ContentLength <= 0) throw new ArgumentException("file is empty");
            if (file.ContentLength > 10 * 1024 * 1024) throw new ArgumentException("file exceeds 10MB");

            var ext = (Path.GetExtension(file.FileName) ?? string.Empty).ToLowerInvariant();

            if (ext == ".pdf")
            {
                var baseDir = HttpContext.Current.Server.MapPath("~\\" + UploadRoot);
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

                var relDir = Path.Combine(UploadRoot, subDir ?? string.Empty);
                var absDir = HttpContext.Current.Server.MapPath("~\\" + relDir);
                Directory.CreateDirectory(absDir);

                var fileName = Guid.NewGuid().ToString("N") + ".pdf";
                var absPath = Path.Combine(absDir, fileName);
                file.SaveAs(absPath);

                var relPath = Path.Combine(relDir, fileName).Replace("\\", "/");
                var size = new FileInfo(absPath).Length;
                return new SavedFile { RelativePath = relPath, FileName = fileName, SizeBytes = size };
            }

            // images: reuse SavePhoto (auto resized + convert to JPEG)
            return SavePhoto(file, subDir);
        }
        private static void SaveResizedJpeg(Image img, string absPath, int maxWidth)
        {
            int w = img.Width;
            int h = img.Height;

            if (w <= maxWidth)
            {
                SaveJpeg(img, absPath, 90L);
                return;
            }

            var newW = maxWidth;
            var newH = (int)(h * (newW / (double)w));

            using (var bmp = new Bitmap(newW, newH))
            {
                bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, 0, 0, newW, newH);
                }
                SaveJpeg(bmp, absPath, 90L);
            }
        }

        private static void SaveJpeg(Image img, string absPath, long quality)
        {
            var codec = GetEncoder(ImageFormat.Jpeg);
            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            img.Save(absPath, codec, ep);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var c in codecs)
            {
                if (c.FormatID == format.Guid) return c;
            }
            return null;
        }

        public class SavedFile
        {
            public string RelativePath { get; set; }
            public string FileName { get; set; }
            public long SizeBytes { get; set; }
        }
    }
}
