using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Conversions.cs — le moteur : familles de formats (images, audio, textes),
// dossier de dépendances partagé, recherche des outils (FFmpeg, ImageMagick,
// Pandoc) et les conversions elles-mêmes (GDI+ natif, ICO et PDF maison).

namespace SkadooshConverter
{
    // ------------------------------------------------- moteur de conversion

    public enum Categorie { Aucune, Images, Audio, Texte }

    public static class Conversions
    {
        public static readonly HashSet<string> ExtsImages =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff", "ico" };
        // Formats modernes que GDI+ ne sait pas lire : ils passent par
        // ImageMagick (photos iPhone en HEIC, WebP et AVIF du web).
        public static readonly HashSet<string> ExtsImagesMagick =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "heic", "heif", "webp", "avif" };
        public static readonly HashSet<string> ExtsAudio =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "mp3", "wav", "flac", "ogg", "oga", "m4a", "aac", "wma", "opus", "aiff" };
        // Formats texte : convertis par Pandoc (le yt-dlp du texte —
        // codename « Passe-partout » devenu la famille Textes de Skadoosh).
        public static readonly HashSet<string> ExtsTexte =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "docx", "odt", "md", "markdown", "rtf", "html", "htm", "epub", "txt", "tex", "rst" };

        public static readonly string[] CiblesImages =
            new string[] { "png", "jpg", "webp", "bmp", "gif", "tiff", "ico", "pdf" };
        public static readonly string[] CiblesAudio =
            new string[] { "mp3", "wav", "flac", "ogg", "m4a", "opus" };
        public static readonly string[] CiblesTexte =
            new string[] { "docx", "odt", "md", "rtf", "html", "epub", "txt" };

        public static Categorie CategorieDe(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.');
            if (ExtsImages.Contains(ext) || ExtsImagesMagick.Contains(ext))
                return Categorie.Images;
            if (ExtsAudio.Contains(ext)) return Categorie.Audio;
            if (ExtsTexte.Contains(ext)) return Categorie.Texte;
            return Categorie.Aucune;
        }

        // Cette conversion a-t-elle besoin d'ImageMagick ? (source moderne
        // illisible par GDI+, ou cible WebP que GDI+ ne sait pas écrire)
        public static bool NecessiteMagick(string source, string cible)
        {
            if (ExtsImagesMagick.Contains(Path.GetExtension(source).TrimStart('.')))
                return true;
            return cible == "webp";
        }

        // Même format des deux côtés ? (jpeg == jpg, etc.)
        public static bool MemeFormat(string ext, string cible)
        {
            ext = ext.TrimStart('.').ToLowerInvariant();
            cible = cible.ToLowerInvariant();
            if (ext == cible) return true;
            if ((ext == "jpeg" && cible == "jpg") || (ext == "jpg" && cible == "jpeg")) return true;
            if ((ext == "tif" && cible == "tiff") || (ext == "tiff" && cible == "tif")) return true;
            if ((ext == "heic" && cible == "heif") || (ext == "heif" && cible == "heic")) return true;
            if ((ext == "markdown" && cible == "md") || (ext == "md" && cible == "markdown")) return true;
            if ((ext == "htm" && cible == "html") || (ext == "html" && cible == "htm")) return true;
            return false;
        }

        // Le dossier de dépendances, PARTAGÉ par toutes les apps de la
        // famille Stargazer (un moteur n'est jamais téléchargé deux fois) :
        // STARGAZER_DEPS s'il est donné, sinon <hub>\dependencies quand l'app
        // vit dans Stargazer (Stargazer.exe deux crans au-dessus), sinon
        // %LOCALAPPDATA%\Stargazer\dependencies (app autonome).
        public static string DossierDependances(string appDir)
        {
            var env = Environment.GetEnvironmentVariable("STARGAZER_DEPS");
            if (!string.IsNullOrEmpty(env)) return env;
            try
            {
                var hub = Path.GetDirectoryName(Path.GetDirectoryName(appDir));
                if (hub != null && File.Exists(Path.Combine(hub, "Stargazer.exe")))
                    return Path.Combine(hub, "dependencies");
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine("Stargazer", "dependencies"));
        }

        // Recherche : dossier partagé, puis bin\ local (installations
        // d'avant le partage), puis le PATH.
        private static string TrouverOutil(string appDir, string exe)
        {
            var deps = Path.Combine(DossierDependances(appDir), exe);
            if (File.Exists(deps)) return deps;
            var local = Path.Combine(Path.Combine(appDir, "bin"), exe);
            if (File.Exists(local)) return local;
            return ChercherSurPath(exe);
        }

        public static string TrouverFFmpeg(string appDir)
        {
            return TrouverOutil(appDir, "ffmpeg.exe");
        }

        public static string TrouverMagick(string appDir)
        {
            return TrouverOutil(appDir, "magick.exe");
        }

        public static string TrouverPandoc(string appDir)
        {
            return TrouverOutil(appDir, "pandoc.exe");
        }

        private static string ChercherSurPath(string exe)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (path == null) return null;
            foreach (var dir in path.Split(';'))
            {
                if (dir.Trim().Length == 0) continue;
                try
                {
                    var full = Path.Combine(dir.Trim(), exe);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
            return null;
        }

        // « chanson.mp3 » existe déjà -> « chanson (1).mp3 », etc.
        public static string DestinationUnique(string folder, string fileName)
        {
            var candidate = Path.Combine(folder, fileName);
            if (!File.Exists(candidate)) return candidate;
            var stem = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var n = 1;
            while (true)
            {
                candidate = Path.Combine(folder,
                    string.Format("{0} ({1}){2}", stem, n, ext));
                if (!File.Exists(candidate)) return candidate;
                n++;
            }
        }

        // ------------------------------ images (ImageMagick)

        public static void ConvertirImageMagick(string magick, string source,
            string dest, string cible)
        {
            // Cibles que magick écrit directement ; pour ICO et PDF, on passe
            // par un PNG temporaire puis par notre pipeline natif (icône 256,
            // écrivain PDF maison), pour un rendu identique au reste.
            if (cible == "ico" || cible == "pdf")
            {
                var tempPng = Path.Combine(Path.GetTempPath(),
                    "skadoosh_" + Guid.NewGuid().ToString("N") + ".png");
                try
                {
                    LancerMagick(magick, source, tempPng);
                    using (var img = new Bitmap(tempPng))
                    {
                        if (cible == "ico") SauverIco(img, dest);
                        else SauverPdf(img, dest);
                    }
                }
                finally
                {
                    try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
                }
                return;
            }
            LancerMagick(magick, source, dest);
        }

        private static void LancerMagick(string magick, string source, string dest)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = magick;
            // [0] : ne garder que la première image (HEIC/AVIF multi-images).
            psi.Arguments = "\"" + source + "[0]\" \"" + dest + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                var err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0 || !File.Exists(dest))
                    throw new Exception("ImageMagick a échoué" +
                        (err.Trim().Length > 0 ? " : " + Premiereligne(err) : "."));
            }
        }

        // ------------------------------ images (natif GDI+)

        public static void ConvertirImage(string source, string dest, string cible)
        {
            using (var img = new Bitmap(source))
            {
                if (cible == "ico") { SauverIco(img, dest); return; }
                if (cible == "pdf") { SauverPdf(img, dest); return; }
                if (cible == "jpg")
                {
                    // Fond blanc sous les zones transparentes, sinon elles
                    // deviennent noires en JPEG.
                    using (var flat = new Bitmap(img.Width, img.Height))
                    using (var g = Graphics.FromImage(flat))
                    {
                        g.Clear(Color.White);
                        g.DrawImage(img, 0, 0, img.Width, img.Height);
                        SauverJpeg(flat, dest, 90L);
                    }
                    return;
                }
                ImageFormat format;
                if (cible == "png") format = ImageFormat.Png;
                else if (cible == "bmp") format = ImageFormat.Bmp;
                else if (cible == "gif") format = ImageFormat.Gif;
                else if (cible == "tiff") format = ImageFormat.Tiff;
                else throw new Exception("format cible inconnu : " + cible);
                img.Save(dest, format);
            }
        }

        private static void SauverJpeg(Bitmap img, string dest, long qualite)
        {
            ImageCodecInfo codec = null;
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.FormatID == ImageFormat.Jpeg.Guid) codec = c;
            var prms = new EncoderParameters(1);
            prms.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, qualite);
            img.Save(dest, codec, prms);
        }

        // ICO : une entrée PNG 256x256 (l'image est réduite si besoin,
        // centrée sur fond transparent).
        public static void SauverIco(Bitmap img, string dest)
        {
            var side = 256;
            byte[] png;
            using (var canvas = new Bitmap(side, side))
            using (var g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);
                var scale = Math.Min((float)side / img.Width, (float)side / img.Height);
                if (scale > 1f) scale = 1f;
                var w = (int)(img.Width * scale);
                var h = (int)(img.Height * scale);
                g.DrawImage(img, (side - w) / 2, (side - h) / 2, w, h);
                using (var ms = new MemoryStream())
                {
                    canvas.Save(ms, ImageFormat.Png);
                    png = ms.ToArray();
                }
            }
            using (var outStream = new FileStream(dest, FileMode.Create))
            using (var bw = new BinaryWriter(outStream))
            {
                bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)1);
                bw.Write((byte)0); bw.Write((byte)0);      // 0 => 256
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write((uint)png.Length); bw.Write((uint)22);
                bw.Write(png);
            }
        }

        // PDF minimal : une page à la taille de l'image, qui embarque le
        // JPEG tel quel (filtre DCTDecode) - aucun composant externe.
        public static void SauverPdf(Bitmap img, string dest)
        {
            byte[] jpeg;
            using (var flat = new Bitmap(img.Width, img.Height))
            using (var g = Graphics.FromImage(flat))
            {
                g.Clear(Color.White);
                g.DrawImage(img, 0, 0, img.Width, img.Height);
                using (var ms = new MemoryStream())
                {
                    ImageCodecInfo codec = null;
                    foreach (var c in ImageCodecInfo.GetImageEncoders())
                        if (c.FormatID == ImageFormat.Jpeg.Guid) codec = c;
                    var prms = new EncoderParameters(1);
                    prms.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, 90L);
                    flat.Save(ms, codec, prms);
                    jpeg = ms.ToArray();
                }
            }

            // Dimensions de page en points PDF (1 px à 96 dpi = 0,75 pt).
            var wPt = (img.Width * 72.0 / 96.0).ToString("0.##", CultureInfo.InvariantCulture);
            var hPt = (img.Height * 72.0 / 96.0).ToString("0.##", CultureInfo.InvariantCulture);
            var contenu = "q\n" + wPt + " 0 0 " + hPt + " 0 0 cm\n/Im1 Do\nQ\n";
            var contenuBytes = Encoding.ASCII.GetBytes(contenu);

            using (var ms = new MemoryStream())
            {
                var offsets = new long[6];
                Action<string> w = delegate(string s)
                {
                    var b = Encoding.ASCII.GetBytes(s);
                    ms.Write(b, 0, b.Length);
                };

                w("%PDF-1.4\n");
                offsets[1] = ms.Position;
                w("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
                offsets[2] = ms.Position;
                w("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
                offsets[3] = ms.Position;
                w("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 " + wPt + " " + hPt +
                  "] /Resources << /XObject << /Im1 4 0 R >> >> /Contents 5 0 R >> endobj\n");
                offsets[4] = ms.Position;
                w("4 0 obj << /Type /XObject /Subtype /Image /Width " + img.Width +
                  " /Height " + img.Height +
                  " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " +
                  jpeg.Length + " >> stream\n");
                ms.Write(jpeg, 0, jpeg.Length);
                w("\nendstream endobj\n");
                offsets[5] = ms.Position;
                w("5 0 obj << /Length " + contenuBytes.Length + " >> stream\n");
                ms.Write(contenuBytes, 0, contenuBytes.Length);
                w("endstream endobj\n");

                var xref = ms.Position;
                w("xref\n0 6\n0000000000 65535 f \n");
                for (var i = 1; i <= 5; i++)
                    w(offsets[i].ToString("0000000000") + " 00000 n \n");
                w("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");

                File.WriteAllBytes(dest, ms.ToArray());
            }
        }

        // ------------------------------ audio (FFmpeg)

        public static void ConvertirAudio(string ffmpeg, string source, string dest)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = ffmpeg;
            psi.Arguments = "-y -hide_banner -loglevel error -i \"" + source + "\" \"" + dest + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                var err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0 || !File.Exists(dest))
                    throw new Exception("FFmpeg a échoué" +
                        (err.Trim().Length > 0 ? " : " + Premiereligne(err) : "."));
            }
        }

        // ------------------------------ textes (Pandoc)

        public static void ConvertirTexte(string pandoc, string source,
            string dest, string cible)
        {
            // pandoc déduit le format d'entrée de l'extension ; deux
            // exceptions : .txt (traité comme markdown) et la sortie .txt
            // (format « plain »). --standalone produit des documents complets.
            var sortie = cible == "txt" ? "plain"
                : (cible == "md" ? "markdown" : cible);
            var entree = "";
            var extSource = Path.GetExtension(source).TrimStart('.').ToLowerInvariant();
            if (extSource == "txt") entree = "-f markdown ";

            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = pandoc;
            psi.Arguments = "--standalone " + entree + "-t " + sortie +
                " -o \"" + dest + "\" \"" + source + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardError = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                var err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0 || !File.Exists(dest))
                    throw new Exception("Pandoc a échoué" +
                        (err.Trim().Length > 0 ? " : " + Premiereligne(err) : "."));
            }
        }

        private static string Premiereligne(string s)
        {
            var lines = s.Trim().Split('\n');
            return lines[0].Trim();
        }
    }
}
