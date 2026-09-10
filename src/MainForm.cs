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

// MainForm.cs — la fenêtre : la liste des fichiers, la famille détectée, le
// format cible, et le grand bouton « Skadoosh ! ».

namespace SkadooshConverter
{
    // ------------------------------------------------------------ fenêtre

    public class MainForm : Form
    {
        private RoundedList _filesList;
        private RoundedButton _addButton;
        private RoundedButton _clearButton;
        private Label _categoryLabel;
        private RoundedCombo _targetCombo;
        private RoundedButton _folderButton;   // « Ouvrir le dossier » : le lot vient d'un seul dossier
        private ToolStripMenuItem _aide;
        private ToolStripMenuItem _verifierMaj;
        private Updater.Info _maj;        // la mise à jour trouvée au lancement, s'il y en a une
        private RoundedButton _convertButton;
        private ProgressBar _progress;
        private Label _status;
        private BackgroundWorker _worker;

        private readonly List<string> _files = new List<string>();
        private Categorie _categorie = Categorie.Aucune;
        private readonly string _appDir;

        public MainForm(List<string> initialFiles)
        {
            Text = "Skadoosh converter";
            ClientSize = new Size(600, 496);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            BackColor = Theme.Nuit;
            ForeColor = Theme.Texte;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            _appDir = Path.GetDirectoryName(Application.ExecutablePath);

            // la barre de menus : Fichier, Aide (mises à jour, à propos)
            var menu = new MenuStrip();
            var fichier = new ToolStripMenuItem("Fichier");
            fichier.DropDownItems.Add(Menus.Entree("Ajouter des fichiers…", Keys.Control | Keys.O,
                delegate { OnAdd(this, EventArgs.Empty); }));
            fichier.DropDownItems.Add(Menus.Entree("Vider la liste", Keys.None,
                delegate { ViderListe(); }));
            fichier.DropDownItems.Add(new ToolStripSeparator());
            fichier.DropDownItems.Add(Menus.Entree("Quitter", Keys.None, delegate { Close(); }));
            _aide = new ToolStripMenuItem("Aide");
            _verifierMaj = Menus.Entree("Vérifier les mises à jour…", Keys.None,
                delegate { VerifierMisesAJour(); });
            _aide.DropDownItems.Add(_verifierMaj);
            _aide.DropDownItems.Add(new ToolStripSeparator());
            _aide.DropDownItems.Add(Menus.Entree("À propos de Skadoosh converter", Keys.None,
                delegate { APropos(); }));
            menu.Items.Add(fichier);
            menu.Items.Add(_aide);
            Menus.Styler(menu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            // Les moteurs (FFmpeg, ImageMagick, Pandoc) s'installent tout
            // seuls au lancement quand ils manquent : plus d'étiquette
            // d'état ni de bouton — juste la promesse de la maison.
            var intro = new Label();
            intro.Text = "Images, audio et textes — déposez, choisissez un " +
                "format, skadoosh. Les moteurs se téléchargent tout seuls " +
                "au premier besoin.";
            intro.SetBounds(16, 38, 568, 36);
            intro.ForeColor = Theme.TexteDoux;

            var divider = new Label();
            divider.SetBounds(16, 84, 568, 1);
            divider.BackColor = Theme.Bordure;

            var filesLabel = new Label();
            filesLabel.Text = "Fichiers à convertir (même famille : images, audio ou textes) :";
            filesLabel.SetBounds(16, 96, 450, 20);

            _filesList = new RoundedList();
            _filesList.SetBounds(16, 118, 460, 130);
            _filesList.ListBox.SelectionMode = SelectionMode.MultiExtended;

            _addButton = new RoundedButton();
            _addButton.Text = "Ajouter…";
            _addButton.Icone = "ouvrir";
            _addButton.SetBounds(484, 118, 100, 32);
            Theme.StyleButton(_addButton, false);
            _addButton.Click += OnAdd;

            _clearButton = new RoundedButton();
            _clearButton.Text = "Vider";
            _clearButton.Icone = "corbeille";
            _clearButton.SetBounds(484, 158, 100, 32);
            Theme.StyleButton(_clearButton, false);
            _clearButton.Click += delegate(object s, EventArgs e) { ViderListe(); };

            _categoryLabel = new Label();
            _categoryLabel.SetBounds(16, 256, 300, 20);
            _categoryLabel.ForeColor = Theme.TexteDoux;

            var targetLabel = new Label();
            targetLabel.Text = "Convertir en :";
            targetLabel.SetBounds(16, 290, 100, 22);

            _targetCombo = new RoundedCombo();
            _targetCombo.SetBounds(118, 285, 160, 30);
            _targetCombo.Enabled = false;

            // Les convertis naissent à côté des originaux : quand tout le lot
            // vient du même dossier, c'est le dossier de destination.
            _folderButton = new RoundedButton();
            _folderButton.Text = "Ouvrir le dossier";
            _folderButton.Icone = "ouvrir";
            _folderButton.SetBounds(424, 284, 160, 32);
            Theme.StyleButton(_folderButton, false);
            _folderButton.Visible = false;
            _folderButton.Click += delegate(object s, EventArgs e) { OuvrirDossier(); };

            _convertButton = new RoundedButton();
            _convertButton.Text = "Skadoosh !";
            _convertButton.SetBounds(16, 328, 568, 42);
            _convertButton.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            Theme.StyleButton(_convertButton, true);
            _convertButton.Click += OnConvert;

            _progress = new ProgressBar();
            _progress.SetBounds(16, 386, 568, 16);

            _status = new Label();
            _status.SetBounds(16, 410, 568, 36);
            _status.Text = "Ajoutez des fichiers, choisissez un format, et… skadoosh.";
            _status.ForeColor = Theme.TexteDoux;
            _status.AutoEllipsis = true;

            var hint = new Label();
            hint.Text = "Les fichiers convertis sont créés à côté des originaux (jamais écrasés).";
            hint.SetBounds(16, 452, 568, 20);
            hint.ForeColor = Theme.TexteDoux;

            Controls.Add(intro);
            Controls.Add(divider);
            Controls.Add(filesLabel);
            Controls.Add(_filesList);
            Controls.Add(_addButton);
            Controls.Add(_clearButton);
            Controls.Add(_categoryLabel);
            Controls.Add(targetLabel);
            Controls.Add(_targetCombo);
            Controls.Add(_folderButton);
            Controls.Add(_convertButton);
            Controls.Add(_progress);
            Controls.Add(_status);
            Controls.Add(hint);

            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.DoWork += ConvertDoWork;
            _worker.ProgressChanged += ConvertProgress;
            _worker.RunWorkerCompleted += ConvertCompleted;

            MajCategorie();

            if (initialFiles != null)
                foreach (var f in initialFiles) AjouterFichier(f, false);
            MajCategorie();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Sombre(this);
        }

        // ------------------------------------------------- mises à jour

        // Au lancement : vérification silencieuse en arrière-plan ; s'il y
        // a plus récent, le menu Aide porte un point et le statut le dit.
        private void VerifierMisesAJourEnFond()
        {
            var t = new System.Threading.Thread(delegate()
            {
                Updater.Info info;
                try { info = Updater.Verifier(); }
                catch { return; }
                if (!Updater.PlusRecente(info.Version, Updater.VersionLocale(_appDir))) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed) return;
                        _maj = info;
                        _aide.Text = "Aide  ●";
                        _aide.ForeColor = Theme.OrClair;
                        _verifierMaj.Text = "Installer la version " + info.Version + "…";
                        if (!_worker.IsBusy)
                        {
                            _status.Text = Updater.Nom + " " + info.Version + " est disponible — menu Aide.";
                            _status.ForeColor = Theme.OrClair;
                        }
                    });
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        // Menu Aide > Vérifier les mises à jour.
        private void VerifierMisesAJour()
        {
            var info = _maj;
            if (info == null)
            {
                Cursor = Cursors.WaitCursor;
                try { info = Updater.Verifier(); }
                catch (Exception ex)
                {
                    Cursor = Cursors.Default;
                    MessageDialog.Show(this, "Impossible de vérifier les mises à jour : " + ex.Message,
                        "Mises à jour", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Cursor = Cursors.Default;
                if (!Updater.PlusRecente(info.Version, Updater.VersionLocale(_appDir)))
                {
                    MessageDialog.Show(this, "Vous avez la dernière version de " + Updater.Nom + " (" +
                        Updater.VersionLocale(_appDir) + ").", "Mises à jour",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            using (var d = new MiseAJourDialog(info, _appDir))
                d.ShowDialog(this);
        }

        private void APropos()
        {
            MessageDialog.Show(this, Updater.Nom + " " + Updater.VersionLocale(_appDir) +
                " — convertit images, audio et textes d'un format à l'autre.\n\n" +
                "Une application de la famille Stargazer, par Rémi Escamilla.\n" +
                "github.com/" + Updater.Depot, "À propos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ------------------------------------------------------- dépendances
        // Plus de bouton ni d'étiquette d'état : ce qui manque se télécharge
        // tout seul au lancement (FenetreInstallation), et un dernier filet
        // au moment de convertir couvre le cas « installé plus tard ».

        private bool _installationFaite;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_installationFaite) return;
            _installationFaite = true;
            VerifierMisesAJourEnFond();
            InstallerManquantes();
        }

        // Ouvre la fenêtre d'installation pour tout moteur absent.
        private void InstallerManquantes()
        {
            var scripts = Path.Combine(_appDir, "scripts");
            var deps = new List<string[]>();
            if (Conversions.TrouverFFmpeg(_appDir) == null)
                deps.Add(new string[] { "FFmpeg (audio)",
                    Path.Combine(scripts, "install-ffmpeg.ps1") });
            if (Conversions.TrouverMagick(_appDir) == null)
                deps.Add(new string[] { "ImageMagick (HEIC, WebP, AVIF)",
                    Path.Combine(scripts, "install-magick.ps1") });
            if (Conversions.TrouverPandoc(_appDir) == null)
                deps.Add(new string[] { "Pandoc (textes)",
                    Path.Combine(scripts, "install-pandoc.ps1") });
            if (Conversions.TrouverTypst(_appDir) == null)
                deps.Add(new string[] { "Typst (textes en PDF)",
                    Path.Combine(scripts, "install-typst.ps1") });
            if (deps.Count == 0) return;
            using (var dlg = new FenetreInstallation(_appDir, deps))
                dlg.ShowDialog(this);
        }

        // ---------------------------------------------------------- fichiers

        private void OnAdd(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Choisissez les fichiers à convertir";
                dlg.Multiselect = true;
                // Lot commencé ? Le dialogue ne montre plus que sa famille :
                // un .jpeg n'a rien à faire dans un lot de .docx.
                if (_categorie == Categorie.Aucune)
                    dlg.Filter = "Tous les fichiers convertibles|" + Conversions.MotifsDe(Categorie.Aucune) +
                        "|Images|" + Conversions.MotifsDe(Categorie.Images) +
                        "|Audio|" + Conversions.MotifsDe(Categorie.Audio) +
                        "|Textes|" + Conversions.MotifsDe(Categorie.Texte);
                else
                    dlg.Filter = NomFamille(_categorie) + " (la famille du lot en cours)|" +
                        Conversions.MotifsDe(_categorie);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var rejets = 0;
                foreach (var f in dlg.FileNames)
                    if (!AjouterFichier(f, true)) rejets++;
                MajCategorie();
                if (rejets > 0)
                {
                    MessageDialog.Show(this,
                        rejets + " fichier(s) ignoré(s) : tous les fichiers d'un lot doivent " +
                        "appartenir à la même famille (images, audio ou textes).",
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

        private static string NomFamille(Categorie cat)
        {
            if (cat == Categorie.Images) return "Images";
            if (cat == Categorie.Audio) return "Audio";
            if (cat == Categorie.Texte) return "Textes";
            return "aucun fichier";
        }

        private void MajCategorie()
        {
            _targetCombo.Items.Clear();
            string[] cibles = null;
            string nom = NomFamille(_categorie);
            if (_categorie == Categorie.Images) cibles = Conversions.CiblesImages;
            else if (_categorie == Categorie.Audio) cibles = Conversions.CiblesAudio;
            else if (_categorie == Categorie.Texte) { cibles = Conversions.CiblesTexte; nom += " (Pandoc)"; }

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
            _folderButton.Visible = Conversions.DossierCommun(_files) != null;
        }

        // Le dossier commun du lot, dans l'Explorateur.
        private void OuvrirDossier()
        {
            var dossier = Conversions.DossierCommun(_files);
            if (dossier == null || !Directory.Exists(dossier)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", "\"" + dossier + "\""); }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Impossible d'ouvrir le dossier : " + ex.Message,
                    "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            public string Pandoc;
            public string Typst;
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
                MessageDialog.Show(this, "Ajoutez d'abord des fichiers à convertir.",
                    "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var args = new BatchArgs();
            args.Files = new List<string>(_files);
            args.Cat = _categorie;
            args.Cible = (string)_targetCombo.SelectedItem;
            args.Ffmpeg = Conversions.TrouverFFmpeg(_appDir);
            args.Magick = Conversions.TrouverMagick(_appDir);
            args.Pandoc = Conversions.TrouverPandoc(_appDir);
            args.Typst = Conversions.TrouverTypst(_appDir);

            // Dernier filet : un moteur manque encore (installation sautée ou
            // ratée au lancement) ? On le télécharge maintenant, directement.
            var besoinMagick = false;
            if (args.Cat == Categorie.Images && args.Magick == null)
                foreach (var f in args.Files)
                    if (Conversions.NecessiteMagick(f, args.Cible)) { besoinMagick = true; break; }
            var besoinTypst = args.Cat == Categorie.Texte && args.Cible == "pdf";
            if ((args.Cat == Categorie.Texte && args.Pandoc == null) ||
                (besoinTypst && args.Typst == null) ||
                (args.Cat == Categorie.Audio && args.Ffmpeg == null) || besoinMagick)
            {
                InstallerManquantes();
                args.Ffmpeg = Conversions.TrouverFFmpeg(_appDir);
                args.Magick = Conversions.TrouverMagick(_appDir);
                args.Pandoc = Conversions.TrouverPandoc(_appDir);
                args.Typst = Conversions.TrouverTypst(_appDir);
                var toujoursAbsent =
                    (args.Cat == Categorie.Texte && args.Pandoc == null) ||
                    (besoinTypst && args.Typst == null) ||
                    (args.Cat == Categorie.Audio && args.Ffmpeg == null) ||
                    (besoinMagick && args.Magick == null);
                if (toujoursAbsent)
                {
                    _status.Text = "Le moteur n'a pas pu être installé (connexion ?) " +
                        "— détails dans logs\\install.log.";
                    _status.ForeColor = Theme.Erreur;
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
                    else if (args.Cat == Categorie.Texte)
                        Conversions.ConvertirTexte(args.Pandoc, args.Typst, source, dest, args.Cible);
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
                // le résumé dans la boîte, la liste complète dans les détails
                var show = Math.Min(result.Errors.Count, 4);
                var msg = "Certaines conversions ont échoué :\n";
                for (var i = 0; i < show; i++) msg += "\n- " + result.Errors[i];
                if (result.Errors.Count > show)
                    msg += "\n… et " + (result.Errors.Count - show) + " de plus (voir les détails).";
                MessageDialog.Show(this, msg, "Skadoosh converter",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    string.Join("\r\n", result.Errors.ToArray()));
            }
        }

        private void SetBusy(bool busy)
        {
            _addButton.Enabled = !busy;
            _clearButton.Enabled = !busy;
            _targetCombo.Enabled = !busy && _targetCombo.Items.Count > 0;
            _folderButton.Enabled = !busy;
            _convertButton.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }
    }
}
