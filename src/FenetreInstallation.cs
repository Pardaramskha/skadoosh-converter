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

// FenetreInstallation.cs — « Premiers préparatifs » : un moteur manque ? Il
// se télécharge tout seul au lancement (scripts\install-*.ps1, dossier de
// dépendances partagé par la famille Stargazer).

namespace SkadooshConverter
{
    // ----------------------------------- installation automatique des moteurs
    // Un moteur manque ? Cette fenêtre s'ouvre au lancement et le télécharge
    // directement — aucune question posée, ça ne se produit qu'une fois.
    // « Continuer en arrière-plan » rend la main pendant le téléchargement.

    public class FenetreInstallation : Form
    {
        private class Dep
        {
            public string Nom;
            public string Script;
            public Label Ligne;
            public int Etat;   // 0 en attente, 1 en cours, 2 ok, 3 échec
        }

        private readonly List<Dep> _deps = new List<Dep>();
        private readonly string _appDir;
        private readonly BackgroundWorker _worker;
        private readonly ProgressBar _barre;
        private readonly RoundedButton _bouton;
        private readonly Label _intro;
        private bool _lance;

        // deps : paires { nom lisible, chemin du script d'installation }.
        public FenetreInstallation(string appDir, List<string[]> deps)
        {
            _appDir = appDir;
            Theme.Dialogue(this, "Premiers préparatifs");
            ControlBox = false;
            ClientSize = new Size(460, 172 + deps.Count * 26);

            _intro = new Label();
            _intro.Text = "Un moteur manque : téléchargement en cours, rien à " +
                "faire de votre côté. Une fois suffit — les prochains " +
                "lancements seront directs.";
            _intro.ForeColor = Theme.TexteDoux;
            _intro.SetBounds(20, 14, 420, 40);

            var y = 62;
            foreach (var d in deps)
            {
                var dep = new Dep();
                dep.Nom = d[0];
                dep.Script = d[1];
                dep.Ligne = new Label();
                dep.Ligne.Text = "…  " + dep.Nom;
                dep.Ligne.ForeColor = Theme.TexteDoux;
                dep.Ligne.SetBounds(28, y, 412, 22);
                Controls.Add(dep.Ligne);
                _deps.Add(dep);
                y += 26;
            }

            _barre = new ProgressBar();
            _barre.SetBounds(20, y + 8, 420, 8);
            _barre.Style = ProgressBarStyle.Marquee;

            _bouton = new RoundedButton();
            _bouton.Text = "Continuer en arrière-plan";
            _bouton.SetBounds(240, y + 30, 200, 32);
            Theme.StyleButton(_bouton, false);
            _bouton.Click += delegate(object s, EventArgs e) { Close(); };

            Controls.Add(_intro);
            Controls.Add(_barre);
            Controls.Add(_bouton);

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += Travailler;
            _worker.ProgressChanged += Avancer;
            _worker.RunWorkerCompleted += Terminer;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Sombre(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_lance) return;
            _lance = true;
            _worker.RunWorkerAsync();
        }

        private void Travailler(object sender, DoWorkEventArgs e)
        {
            for (var i = 0; i < _deps.Count; i++)
            {
                _worker.ReportProgress(i, 1);
                var code = LancerScript(_deps[i].Script);
                _worker.ReportProgress(i, code == 0 ? 2 : 3);
            }
        }

        private void Avancer(object sender, ProgressChangedEventArgs e)
        {
            if (IsDisposed) return;
            var dep = _deps[e.ProgressPercentage];
            dep.Etat = (int)e.UserState;
            switch (dep.Etat)
            {
                case 1:
                    dep.Ligne.Text = "⏳  " + dep.Nom + " — téléchargement…";
                    dep.Ligne.ForeColor = Theme.Info;
                    break;
                case 2:
                    dep.Ligne.Text = "✔  " + dep.Nom;
                    dep.Ligne.ForeColor = Theme.Ok;
                    break;
                default:
                    dep.Ligne.Text = "✖  " + dep.Nom + " — échec (connexion ?)";
                    dep.Ligne.ForeColor = Theme.Erreur;
                    break;
            }
        }

        private void Terminer(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed) return;
            var toutBon = true;
            foreach (var d in _deps) if (d.Etat != 2) toutBon = false;
            if (toutBon) { DialogResult = DialogResult.OK; Close(); return; }
            _intro.Text = "Une installation a échoué — vérifiez la connexion. " +
                "Les détails sont dans logs\\install.log ; l'application " +
                "retentera au prochain lancement.";
            _intro.ForeColor = Theme.Erreur;
            _barre.Visible = false;
            _bouton.Text = "Continuer quand même";
        }

        // Script PowerShell caché, toute la sortie journalisée dans
        // logs\install.log (l'échec muet n'a pas le droit d'exister).
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
                    var dir = Path.Combine(_appDir, "logs");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "install.log"),
                        "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " +
                        Path.GetFileName(script) + " — code " + proc.ExitCode +
                        " ===\r\n" + sortie + "\r\n");
                }
                catch { }
                return proc.ExitCode;
            }
        }
    }

}
