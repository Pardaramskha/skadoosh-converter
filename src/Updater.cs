using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

// Updater.cs — « Vérifier les mises à jour » (menu Aide), le standard des
// apps de la famille Stargazer : on interroge la dernière release GitHub
// du dépôt, on compare avec VERSION, et on installe sur place —
// l'archive portable est déballée dans un dossier temporaire, puis un
// petit script cmd attend la fermeture de l'app, recopie les fichiers
// par-dessus (sauf config\, qui appartient à l'utilisateur) et relance
// l'exe. Marche aussi bien pour l'app installée par le Setup, la version
// portable et la copie qui vit dans apps\ de Stargazer.
//
// Au lancement, une vérification silencieuse en arrière-plan : si une
// version plus récente existe, le menu Aide porte un point.
//
// Dépôt privé : l'API répond 404 sans jeton — GITHUB_TOKEN (variable
// d'environnement) est envoyé s'il existe, pratique pour tester avant
// le passage en public.

namespace SkadooshConverter
{
    public static class Updater
    {
        public const string Depot = "Pardaramskha/skadoosh-converter";
        public const string NomZip = "skadoosh-converter-windows-portable.zip";
        public const string Exe = "SkadooshConverter.exe";
        public const string Nom = "Skadoosh converter";

        public class Info
        {
            public string Version;   // « 1.7.0 »
            public string UrlZip;    // l'archive portable Windows de la release
            public string UrlPage;   // la page de la release
            public string Notes;     // le texte de la release (Markdown brut)
        }

        // La version de l'app : le fichier VERSION à côté de l'exe, sinon
        // celle de l'assembly.
        public static string VersionLocale(string appDir)
        {
            try
            {
                var f = Path.Combine(appDir, "VERSION");
                if (File.Exists(f)) return File.ReadAllText(f).Trim();
            }
            catch { }
            var v = Assembly.GetEntryAssembly().GetName().Version;
            return v.Major + "." + v.Minor + "." + v.Build;
        }

        // La dernière release publiée. Lève une exception parlante sinon.
        public static Info Verifier()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            string json;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Skadoosh converter";
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                var jeton = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (!string.IsNullOrEmpty(jeton))
                    wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + jeton;
                wc.Encoding = Encoding.UTF8;
                try
                {
                    json = wc.DownloadString("https://api.github.com/repos/" + Depot + "/releases/latest");
                }
                catch (WebException ex)
                {
                    var rep = ex.Response as HttpWebResponse;
                    if (rep != null && rep.StatusCode == HttpStatusCode.NotFound)
                        throw new Exception("aucune version publiée trouvée (dépôt privé ou sans release).");
                    if (rep != null)
                        throw new Exception("GitHub répond " + (int)rep.StatusCode + " " + rep.StatusDescription + ".");
                    throw new Exception("pas de connexion à GitHub (" + ex.Message + ").");
                }
            }
            var i = new Info();
            i.Version = Champ(json, "tag_name").TrimStart('v', 'V');
            i.UrlPage = Champ(json, "html_url");
            i.Notes = Champ(json, "body");
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
                if (m.Groups[1].Value.EndsWith("/" + NomZip, StringComparison.OrdinalIgnoreCase))
                { i.UrlZip = m.Groups[1].Value; break; }
            if (i.Version.Length == 0) throw new Exception("réponse GitHub illisible.");
            return i;
        }

        // Un champ texte du JSON (premier trouvé), séquences d'échappement
        // rendues — assez pour l'API des releases.
        private static string Champ(string json, string nom)
        {
            var m = Regex.Match(json, "\"" + nom + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) return "";
            return Regex.Replace(m.Groups[1].Value, @"\\(u[0-9a-fA-F]{4}|.)", delegate(Match e)
            {
                var s = e.Groups[1].Value;
                switch (s[0])
                {
                    case 'n': return "\n";
                    case 'r': return "";
                    case 't': return "\t";
                    case 'u': return ((char)int.Parse(s.Substring(1), NumberStyles.HexNumber)).ToString();
                    default: return s;
                }
            });
        }

        // « 1.7.0 » > « 1.6.1 » ?
        public static bool PlusRecente(string distante, string locale)
        {
            var a = Nombres(distante); var b = Nombres(locale);
            for (var i = 0; i < 3; i++)
                if (a[i] != b[i]) return a[i] > b[i];
            return false;
        }

        private static int[] Nombres(string v)
        {
            var r = new int[3];
            var parts = (v ?? "").Split('.');
            for (var i = 0; i < 3 && i < parts.Length; i++)
            {
                var m = Regex.Match(parts[i], @"\d+");
                r[i] = m.Success ? int.Parse(m.Value) : 0;
            }
            return r;
        }

        // Télécharge l'archive, la déballe à côté, puis laisse un script cmd
        // finir le travail une fois l'app fermée (l'exe est verrouillé tant
        // qu'elle tourne). L'appelant ferme l'application juste après.
        public static void Installer(Info info, string appDir)
        {
            if (string.IsNullOrEmpty(info.UrlZip))
                throw new Exception("la release ne contient pas " + NomZip + ".");
            var temp = Path.Combine(Path.GetTempPath(), "skadoosh-maj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            var zip = Path.Combine(temp, NomZip);
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Skadoosh converter";
                var jeton = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (!string.IsNullOrEmpty(jeton))
                    wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + jeton;
                wc.DownloadFile(info.UrlZip, zip);
            }
            var contenu = Path.Combine(temp, "contenu");
            Deballer(zip, contenu);
            if (!File.Exists(Path.Combine(contenu, Exe)))
                throw new Exception("l'archive ne contient pas " + Exe + ".");
            // config\ appartient à l'utilisateur (ses ligatures, typo.conf)
            var config = Path.Combine(contenu, "config");
            if (Directory.Exists(config)) Directory.Delete(config, true);

            var script = Path.Combine(temp, "maj.cmd");
            var pid = Process.GetCurrentProcess().Id;
            var lignes = new StringBuilder();
            lignes.Append("@echo off\r\n");
            lignes.Append(":attend\r\n");
            lignes.Append("tasklist /FI \"PID eq " + pid + "\" 2>nul | find \"" + pid + "\" >nul\r\n");
            lignes.Append("if not errorlevel 1 (ping 127.0.0.1 -n 2 >nul & goto attend)\r\n");
            lignes.Append("xcopy \"" + contenu + "\\*\" \"" + appDir.TrimEnd('\\') + "\\\" /E /Y /I /Q >nul\r\n");
            lignes.Append("start \"\" \"" + Path.Combine(appDir, Exe) + "\"\r\n");
            lignes.Append("cd /d \"%TEMP%\"\r\n");
            lignes.Append("rmdir /s /q \"" + temp + "\"\r\n");
            File.WriteAllText(script, lignes.ToString(), Encoding.Default);

            MettreAJourRegistre(appDir, info.Version);

            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.WorkingDirectory = Path.GetTempPath();
            Process.Start(psi);
        }

        // Déballe en refusant les chemins qui sortent du dossier.
        private static void Deballer(string zip, string dossier)
        {
            Directory.CreateDirectory(dossier);
            var racine = Path.GetFullPath(dossier).TrimEnd('\\') + "\\";
            using (var a = ZipFile.OpenRead(zip))
                foreach (var e in a.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    var cible = Path.GetFullPath(Path.Combine(dossier, e.FullName.Replace('/', '\\')));
                    if (!cible.StartsWith(racine, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(cible));
                    e.ExtractToFile(cible, true);
                }
        }

        // Si l'app a été posée par le Setup, Paramètres > Applications
        // installées doit afficher la nouvelle version.
        private static void MettreAJourRegistre(string appDir, string version)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SkadooshConverter", true))
                {
                    if (k == null) return;
                    var lieu = k.GetValue("InstallLocation") as string;
                    if (lieu == null || !string.Equals(Path.GetFullPath(lieu).TrimEnd('\\'),
                        Path.GetFullPath(appDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                    k.SetValue("DisplayVersion", version);
                }
            }
            catch { }
        }
    }

    // La proposition : « Skadoosh converter X est disponible », les notes de la
    // release, et « Installer et redémarrer ».
    public class MiseAJourDialog : Form
    {
        private readonly Updater.Info _info;
        private readonly string _appDir;
        private readonly Label _etat;
        private readonly ProgressBar _barre;
        private readonly RoundedButton _installer;
        private readonly RoundedButton _plusTard;

        public MiseAJourDialog(Updater.Info info, string appDir)
        {
            _info = info;
            _appDir = appDir;
            Theme.Dialogue(this, "Mise à jour de Skadoosh converter");
            ClientSize = new Size(520, 360);

            var titre = new Label();
            titre.Text = "Skadoosh converter " + info.Version + " est disponible";
            titre.Font = new Font("Segoe UI Semibold", 13f);
            titre.ForeColor = Theme.OrClair;
            titre.SetBounds(24, 18, 472, 28);
            Controls.Add(titre);

            var sous = new Label();
            sous.Text = "Vous avez la " + Updater.VersionLocale(appDir) + ". Ce qui change :";
            sous.ForeColor = Theme.TexteDoux;
            sous.SetBounds(24, 48, 472, 20);
            Controls.Add(sous);

            var notes = new RoundedField();
            notes.Multiline = true;
            notes.ReadOnly = true;
            notes.ScrollBars = ScrollBars.Vertical;
            notes.Font = new Font("Segoe UI", 9.5f);
            notes.SetBounds(24, 74, 472, 180);
            notes.Text = (info.Notes ?? "").Replace("\r", "").Replace("\n", "\r\n").Trim();
            if (notes.Text.Length == 0) notes.Text = "(pas de notes pour cette version)";
            Controls.Add(notes);

            _etat = new Label();
            _etat.ForeColor = Theme.TexteDoux;
            _etat.SetBounds(24, 264, 472, 36);
            _etat.Text = "L'archive est téléchargée, l'application se ferme, les fichiers " +
                "sont remplacés (vos réglages restent) et Skadoosh converter redémarre.";
            Controls.Add(_etat);

            _barre = new ProgressBar();
            _barre.Style = ProgressBarStyle.Marquee;
            _barre.SetBounds(24, 304, 472, 6);
            _barre.Visible = false;
            Controls.Add(_barre);

            _plusTard = new RoundedButton();
            _plusTard.Text = "Plus tard";
            _plusTard.SetBounds(224, 316, 96, 32);
            Theme.StyleButton(_plusTard, false);
            _plusTard.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(_plusTard);

            _installer = new RoundedButton();
            _installer.Text = "Installer et redémarrer";
            _installer.SetBounds(328, 316, 168, 32);
            Theme.StyleButton(_installer, true);
            _installer.Click += delegate { Lancer(); };
            Controls.Add(_installer);
            AcceptButton = _installer;
            CancelButton = _plusTard;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Sombre(this);
        }

        private void Lancer()
        {
            _installer.Enabled = false;
            _plusTard.Enabled = false;
            _barre.Visible = true;
            _etat.Text = "Téléchargement de la version " + _info.Version + "…";
            var w = new BackgroundWorker();
            w.DoWork += delegate { Updater.Installer(_info, _appDir); };
            w.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs a)
            {
                if (IsDisposed) return;
                if (a.Error != null)
                {
                    _barre.Visible = false;
                    _installer.Enabled = true;
                    _plusTard.Enabled = true;
                    _etat.ForeColor = Theme.Erreur;
                    _etat.Text = "Impossible : " + a.Error.Message;
                    return;
                }
                _etat.ForeColor = Theme.Ok;
                _etat.Text = "Prête. Skadoosh converter redémarre…";
                DialogResult = DialogResult.OK;
                Application.Exit();
            };
            w.RunWorkerAsync();
        }
    }
}
