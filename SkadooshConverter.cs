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

// Skadoosh converter - édition Windows (.NET Framework / WinForms)
// Convertisseur de fichiers : images (natif GDI+, y compris ICO et PDF)
// et audio (via FFmpeg, installé en arrière-plan d'un clic).
// Habillage sombre de la famille Stargazer.
// Style compatible C# 5 pour compiler avec le csc.exe intégré (aucun SDK requis).

namespace SkadooshConverter
{
    public static class Theme
    {
        public static readonly Color Nuit = Color.FromArgb(11, 16, 38);
        public static readonly Color Panneau = Color.FromArgb(19, 26, 51);
        public static readonly Color Bordure = Color.FromArgb(42, 51, 88);
        public static readonly Color Or = Color.FromArgb(212, 175, 55);
        public static readonly Color Texte = Color.FromArgb(230, 230, 240);
        public static readonly Color TexteDoux = Color.FromArgb(154, 163, 192);
        public static readonly Color Ok = Color.FromArgb(152, 195, 121);
        public static readonly Color Erreur = Color.FromArgb(230, 110, 120);
        public static readonly Color Info = Color.FromArgb(122, 162, 247);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = primary ? Or : Bordure;
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = primary ? Or : Panneau;
            b.ForeColor = primary ? Color.FromArgb(20, 20, 30) : Texte;
            b.Cursor = Cursors.Hand;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    public class RoundedButton : Button
    {
        private bool _hover;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var fill = BackColor;
            if (!Enabled) fill = Theme.Mix(fill, Color.Black, 0.35f);
            else if (_hover) fill = Theme.Mix(fill, Color.White, 0.10f);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                var border = Enabled ? FlatAppearance.BorderColor
                                     : Color.FromArgb(70, 76, 105);
                using (var p = new Pen(border)) g.DrawPath(p, path);
            }

            var tc = Enabled ? ForeColor : Color.FromArgb(120, 126, 150);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
    }

    // ------------------------------------------------- moteur de conversion

    public enum Categorie { Aucune, Images, Audio }

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

        public static readonly string[] CiblesImages =
            new string[] { "png", "jpg", "webp", "bmp", "gif", "tiff", "ico", "pdf" };
        public static readonly string[] CiblesAudio =
            new string[] { "mp3", "wav", "flac", "ogg", "m4a", "opus" };

        public static Categorie CategorieDe(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.');
            if (ExtsImages.Contains(ext) || ExtsImagesMagick.Contains(ext))
                return Categorie.Images;
            if (ExtsAudio.Contains(ext)) return Categorie.Audio;
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
            return false;
        }

        public static string TrouverFFmpeg(string appDir)
        {
            var local = Path.Combine(Path.Combine(appDir, "bin"), "ffmpeg.exe");
            if (File.Exists(local)) return local;
            return ChercherSurPath("ffmpeg.exe");
        }

        public static string TrouverMagick(string appDir)
        {
            var local = Path.Combine(Path.Combine(appDir, "bin"), "magick.exe");
            if (File.Exists(local)) return local;
            return ChercherSurPath("magick.exe");
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

        private static string Premiereligne(string s)
        {
            var lines = s.Trim().Split('\n');
            return lines[0].Trim();
        }
    }

    // ------------------------------------------------------------ fenêtre

    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int value, int size);

        private Label _depsLabel;
        private RoundedButton _depsButton;
        private ListBox _filesList;
        private RoundedButton _addButton;
        private RoundedButton _clearButton;
        private Label _categoryLabel;
        private ComboBox _targetCombo;
        private RoundedButton _convertButton;
        private ProgressBar _progress;
        private Label _status;
        private BackgroundWorker _worker;
        private BackgroundWorker _depsWorker;

        private readonly List<string> _files = new List<string>();
        private Categorie _categorie = Categorie.Aucune;
        private readonly string _appDir;

        public MainForm(List<string> initialFiles)
        {
            Text = "Skadoosh converter";
            ClientSize = new Size(600, 470);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Nuit;
            ForeColor = Theme.Texte;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            _appDir = Path.GetDirectoryName(Application.ExecutablePath);

            _depsLabel = new Label();
            _depsLabel.SetBounds(16, 16, 380, 34);
            _depsLabel.ForeColor = Theme.TexteDoux;

            _depsButton = new RoundedButton();
            _depsButton.Text = "Installer les dépendances";
            _depsButton.SetBounds(404, 14, 180, 32);
            Theme.StyleButton(_depsButton, false);
            _depsButton.Click += OnInstallDeps;

            var divider = new Label();
            divider.SetBounds(16, 58, 568, 1);
            divider.BackColor = Theme.Bordure;

            var filesLabel = new Label();
            filesLabel.Text = "Fichiers à convertir (même famille : images ou audio) :";
            filesLabel.SetBounds(16, 70, 450, 20);

            _filesList = new ListBox();
            _filesList.SetBounds(16, 92, 460, 130);
            _filesList.BackColor = Theme.Panneau;
            _filesList.ForeColor = Theme.Texte;
            _filesList.BorderStyle = BorderStyle.FixedSingle;
            _filesList.SelectionMode = SelectionMode.MultiExtended;
            _filesList.HorizontalScrollbar = true;

            _addButton = new RoundedButton();
            _addButton.Text = "Ajouter…";
            _addButton.SetBounds(484, 92, 100, 30);
            Theme.StyleButton(_addButton, false);
            _addButton.Click += OnAdd;

            _clearButton = new RoundedButton();
            _clearButton.Text = "Vider";
            _clearButton.SetBounds(484, 130, 100, 30);
            Theme.StyleButton(_clearButton, false);
            _clearButton.Click += delegate(object s, EventArgs e) { ViderListe(); };

            _categoryLabel = new Label();
            _categoryLabel.SetBounds(16, 230, 300, 20);
            _categoryLabel.ForeColor = Theme.TexteDoux;

            var targetLabel = new Label();
            targetLabel.Text = "Convertir en :";
            targetLabel.SetBounds(16, 262, 100, 22);

            _targetCombo = new ComboBox();
            _targetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _targetCombo.SetBounds(118, 259, 160, 24);
            _targetCombo.FlatStyle = FlatStyle.Flat;
            _targetCombo.BackColor = Theme.Panneau;
            _targetCombo.ForeColor = Theme.Texte;
            _targetCombo.Enabled = false;

            _convertButton = new RoundedButton();
            _convertButton.Text = "Skadoosh !";
            _convertButton.SetBounds(16, 300, 568, 42);
            _convertButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            Theme.StyleButton(_convertButton, true);
            _convertButton.Click += OnConvert;

            _progress = new ProgressBar();
            _progress.SetBounds(16, 358, 568, 16);

            _status = new Label();
            _status.SetBounds(16, 382, 568, 36);
            _status.Text = "Ajoutez des fichiers, choisissez un format, et… skadoosh.";
            _status.ForeColor = Theme.TexteDoux;
            _status.AutoEllipsis = true;

            var hint = new Label();
            hint.Text = "Les fichiers convertis sont créés à côté des originaux (jamais écrasés).";
            hint.SetBounds(16, 424, 568, 20);
            hint.ForeColor = Theme.TexteDoux;

            Controls.Add(_depsLabel);
            Controls.Add(_depsButton);
            Controls.Add(divider);
            Controls.Add(filesLabel);
            Controls.Add(_filesList);
            Controls.Add(_addButton);
            Controls.Add(_clearButton);
            Controls.Add(_categoryLabel);
            Controls.Add(targetLabel);
            Controls.Add(_targetCombo);
            Controls.Add(_convertButton);
            Controls.Add(_progress);
            Controls.Add(_status);
            Controls.Add(hint);

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += ConvertDoWork;
            _worker.ProgressChanged += ConvertProgress;
            _worker.RunWorkerCompleted += ConvertCompleted;

            _depsWorker = new BackgroundWorker();
            _depsWorker.DoWork += DepsDoWork;
            _depsWorker.RunWorkerCompleted += DepsCompleted;

            // Revérifier les dépendances quand la fenêtre reprend le focus
            // (ex. au retour de l'installeur).
            Activated += delegate(object s, EventArgs e) { MajDeps(); };
            MajDeps();
            MajCategorie();

            if (initialFiles != null)
                foreach (var f in initialFiles) AjouterFichier(f, false);
            MajCategorie();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            var dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, 4);   // barre de titre sombre
        }

        // ------------------------------------------------------- dépendances

        private void MajDeps()
        {
            var ffmpeg = Conversions.TrouverFFmpeg(_appDir) != null;
            var magick = Conversions.TrouverMagick(_appDir) != null;
            _depsLabel.Text = "Images : natif ✔  ·  HEIC/WebP/AVIF : ImageMagick " +
                (magick ? "✔" : "✖") + "  ·  Audio : FFmpeg " + (ffmpeg ? "✔" : "✖");
            _depsLabel.ForeColor = (ffmpeg && magick) ? Theme.Ok : Theme.TexteDoux;
            _depsButton.Visible = !(ffmpeg && magick);
        }

        // Installation des dépendances manquantes en arrière-plan, sans
        // fenêtre de terminal : les scripts PowerShell tournent cachés, le
        // statut s'affiche ici.
        private void OnInstallDeps(object sender, EventArgs e)
        {
            if (_depsWorker.IsBusy) return;
            _depsButton.Enabled = false;
            _status.Text = "Téléchargement des dépendances en arrière-plan… (une à deux minutes)";
            _status.ForeColor = Theme.Info;
            _depsWorker.RunWorkerAsync();
        }

        private string CheminJournalInstall
        {
            get { return Path.Combine(Path.Combine(_appDir, "logs"), "install.log"); }
        }

        // Lance un script d'installation caché et consigne TOUTE sa sortie
        // dans logs\install.log : les terminaux cachés sont la règle de la
        // famille, l'échec muet n'a plus le droit de l'être.
        private int LancerScript(string script)
        {
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"";
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                var sortie = proc.StandardOutput.ReadToEnd() +
                             proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                try
                {
                    var dir = Path.GetDirectoryName(CheminJournalInstall);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(CheminJournalInstall,
                        "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " +
                        Path.GetFileName(script) + " — code " + proc.ExitCode +
                        " ===\r\n" + sortie + "\r\n");
                }
                catch { /* le journal ne doit pas faire échouer l'installation */ }
                return proc.ExitCode;
            }
        }

        private void DepsDoWork(object sender, DoWorkEventArgs e)
        {
            var scripts = Path.Combine(_appDir, "scripts");
            var code = 0;
            if (Conversions.TrouverFFmpeg(_appDir) == null)
                code += LancerScript(Path.Combine(scripts, "install-ffmpeg.ps1"));
            if (Conversions.TrouverMagick(_appDir) == null)
                code += LancerScript(Path.Combine(scripts, "install-magick.ps1"));
            e.Result = code;
        }

        private void DepsCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _depsButton.Enabled = true;
            MajDeps();
            var ok = e.Error == null && (int)e.Result == 0 &&
                Conversions.TrouverFFmpeg(_appDir) != null &&
                Conversions.TrouverMagick(_appDir) != null;
            if (ok)
            {
                _status.Text = "Dépendances installées ! Tout est prêt.";
                _status.ForeColor = Theme.Ok;
            }
            else
            {
                _status.Text = "Installation incomplète — détails dans logs\\install.log.";
                _status.ForeColor = Theme.Erreur;

                // « Voir le journal » : montrer pourquoi, pas juste que ça a raté.
                var extrait = "";
                try
                {
                    var lignes = File.ReadAllLines(CheminJournalInstall);
                    var debut = Math.Max(0, lignes.Length - 12);
                    extrait = string.Join("\n", lignes, debut, lignes.Length - debut).Trim();
                }
                catch { }
                var rep = MessageBox.Show(this,
                    "L'installation des dépendances a échoué.\n\n" +
                    (extrait.Length > 0
                        ? "Dernières lignes du journal :\n\n" + extrait + "\n\n"
                        : "") +
                    "Ouvrir le journal complet ?\n" + CheminJournalInstall,
                    "Skadoosh converter", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (rep == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("notepad.exe",
                            "\"" + CheminJournalInstall + "\"");
                    }
                    catch { }
                }
            }
        }

        // ---------------------------------------------------------- fichiers

        private void OnAdd(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Choisissez les fichiers à convertir";
                dlg.Multiselect = true;
                dlg.Filter = "Tous les fichiers convertibles|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico;" +
                    "*.heic;*.heif;*.webp;*.avif;" +
                    "*.mp3;*.wav;*.flac;*.ogg;*.oga;*.m4a;*.aac;*.wma;*.opus;*.aiff|Tous les fichiers|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var rejets = 0;
                foreach (var f in dlg.FileNames)
                    if (!AjouterFichier(f, true)) rejets++;
                MajCategorie();
                if (rejets > 0)
                {
                    MessageBox.Show(this,
                        rejets + " fichier(s) ignoré(s) : tous les fichiers d'un lot doivent " +
                        "appartenir à la même famille (images ou audio).",
                        "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private bool AjouterFichier(string path, bool prevenir)
        {
            if (!File.Exists(path)) return false;
            var cat = Conversions.CategorieDe(path);
            if (cat == Categorie.Aucune) return false;
            if (_categorie != Categorie.Aucune && cat != _categorie) return false;
            foreach (var f in _files)
                if (string.Equals(f, path, StringComparison.OrdinalIgnoreCase)) return true;
            _categorie = cat;
            _files.Add(path);
            _filesList.Items.Add(Path.GetFileName(path) + "   —   " + Path.GetDirectoryName(path));
            return true;
        }

        private void ViderListe()
        {
            _files.Clear();
            _filesList.Items.Clear();
            _categorie = Categorie.Aucune;
            MajCategorie();
        }

        private void MajCategorie()
        {
            _targetCombo.Items.Clear();
            string[] cibles = null;
            string nom = "aucun fichier";
            if (_categorie == Categorie.Images) { cibles = Conversions.CiblesImages; nom = "Images"; }
            else if (_categorie == Categorie.Audio) { cibles = Conversions.CiblesAudio; nom = "Audio"; }

            _categoryLabel.Text = "Famille détectée : " + nom +
                (_files.Count > 0 ? "  (" + _files.Count + " fichier(s))" : "");
            if (cibles != null)
            {
                foreach (var c in cibles) _targetCombo.Items.Add(c);
                _targetCombo.SelectedIndex = 0;
                _targetCombo.Enabled = true;
            }
            else
            {
                _targetCombo.Enabled = false;
            }
        }

        // -------------------------------------------------------- conversion

        private class BatchArgs
        {
            public List<string> Files;
            public Categorie Cat;
            public string Cible;
            public string Ffmpeg;
            public string Magick;
        }

        private class BatchResult
        {
            public int Converted;
            public int Skipped;
            public List<string> Errors = new List<string>();
        }

        private void OnConvert(object sender, EventArgs e)
        {
            if (_worker.IsBusy) return;
            if (_files.Count == 0 || _targetCombo.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Ajoutez d'abord des fichiers à convertir.",
                    "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var args = new BatchArgs();
            args.Files = new List<string>(_files);
            args.Cat = _categorie;
            args.Cible = (string)_targetCombo.SelectedItem;
            args.Ffmpeg = Conversions.TrouverFFmpeg(_appDir);
            args.Magick = Conversions.TrouverMagick(_appDir);

            if (args.Cat == Categorie.Audio && args.Ffmpeg == null)
            {
                MessageBox.Show(this,
                    "La conversion audio a besoin de FFmpeg. Cliquez sur " +
                    "« Installer les dépendances » d'abord.",
                    "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (args.Cat == Categorie.Images && args.Magick == null)
            {
                var besoin = false;
                foreach (var f in args.Files)
                    if (Conversions.NecessiteMagick(f, args.Cible)) { besoin = true; break; }
                if (besoin)
                {
                    MessageBox.Show(this,
                        "Les formats HEIC/HEIF/WebP/AVIF ont besoin d'ImageMagick. " +
                        "Cliquez sur « Installer les dépendances » d'abord.",
                        "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            SetBusy(true);
            _progress.Value = 0;
            _progress.Maximum = args.Files.Count;
            _worker.RunWorkerAsync(args);
        }

        private void ConvertDoWork(object sender, DoWorkEventArgs e)
        {
            var args = (BatchArgs)e.Argument;
            var result = new BatchResult();
            var done = 0;

            foreach (var source in args.Files)
            {
                done++;
                _worker.ReportProgress(done, Path.GetFileName(source));
                try
                {
                    if (Conversions.MemeFormat(Path.GetExtension(source), args.Cible))
                    {
                        result.Skipped++;
                        continue;
                    }
                    var dest = Conversions.DestinationUnique(
                        Path.GetDirectoryName(source),
                        Path.GetFileNameWithoutExtension(source) + "." + args.Cible);

                    if (args.Cat == Categorie.Images)
                    {
                        if (Conversions.NecessiteMagick(source, args.Cible))
                            Conversions.ConvertirImageMagick(args.Magick, source, dest, args.Cible);
                        else
                            Conversions.ConvertirImage(source, dest, args.Cible);
                    }
                    else
                        Conversions.ConvertirAudio(args.Ffmpeg, source, dest);

                    result.Converted++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(Path.GetFileName(source) + " : " + ex.Message);
                }
            }
            e.Result = result;
        }

        private void ConvertProgress(object sender, ProgressChangedEventArgs e)
        {
            _progress.Value = Math.Min(e.ProgressPercentage, _progress.Maximum);
            _status.Text = "Conversion… " + e.ProgressPercentage + " / " + _progress.Maximum +
                " : " + (e.UserState as string);
            _status.ForeColor = Theme.Info;
        }

        private void ConvertCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            SetBusy(false);
            var result = (BatchResult)e.Result;
            _progress.Value = _progress.Maximum;
            _status.Text = "Skadoosh ! " + result.Converted + " converti(s), " +
                result.Skipped + " déjà au bon format, " + result.Errors.Count + " erreur(s).";
            _status.ForeColor = result.Errors.Count > 0 ? Theme.Erreur : Theme.Ok;

            if (result.Errors.Count > 0)
            {
                var show = Math.Min(result.Errors.Count, 8);
                var msg = "Certaines conversions ont échoué :\n";
                for (var i = 0; i < show; i++) msg += "\n- " + result.Errors[i];
                if (result.Errors.Count > show)
                    msg += "\n… et " + (result.Errors.Count - show) + " de plus.";
                MessageBox.Show(this, msg, "Skadoosh converter",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetBusy(bool busy)
        {
            _addButton.Enabled = !busy;
            _clearButton.Enabled = !busy;
            _targetCombo.Enabled = !busy && _targetCombo.Items.Count > 0;
            _convertButton.Enabled = !busy;
            _depsButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Chemins en argument (glisser-déposer sur l'exe) : pré-remplissent la liste.
            var initial = new List<string>();
            foreach (var a in args)
                if (File.Exists(a)) initial.Add(a);
            Application.Run(new MainForm(initial));
        }
    }
}
