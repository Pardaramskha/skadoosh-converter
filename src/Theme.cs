using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Theme.cs — l habillage de Skadoosh converter : le même langage visuel que le
// reste de la famille Stargazer (Marabook, Markdown we go, My Somehow
// Legal Downloader) — nuit et or, coins arrondis partout, barre de titre
// sombre, et une boîte de dialogue maison à la place de MessageBox.
//
//    Theme          couleurs et petits utilitaires de dessin
//    RoundedButton  bouton arrondi, texte seul ou icône + texte (cf. Icons)
//    RoundedField   zone de texte arrondie (bordure d'or au focus)
//    RoundedList    liste arrondie (les fichiers)
//    RoundedCombo   liste déroulante arrondie (le format cible)
//    MessageDialog  remplaçant de MessageBox : pastille de sens, message,
//                   boutons alignés à droite, le principal en or, et les
//                   détails techniques repliés
//
// Style compatible C# 5 : c'est le compilateur livré avec Windows.

namespace SkadooshConverter
{
    public static class Theme
    {
        public static readonly Color Nuit = Color.FromArgb(11, 16, 38);
        public static readonly Color Panneau = Color.FromArgb(19, 26, 51);
        public static readonly Color Bordure = Color.FromArgb(42, 51, 88);
        public static readonly Color Or = Color.FromArgb(212, 175, 55);
        public static readonly Color OrClair = Color.FromArgb(244, 215, 122);
        public static readonly Color Texte = Color.FromArgb(230, 230, 240);
        public static readonly Color TexteDoux = Color.FromArgb(154, 163, 192);
        public static readonly Color Ok = Color.FromArgb(152, 195, 121);
        public static readonly Color Erreur = Color.FromArgb(230, 110, 120);
        public static readonly Color Info = Color.FromArgb(122, 162, 247);
        public static readonly Color Encre = Color.FromArgb(20, 20, 30);   // texte sur l'or

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        // Barre de titre sombre (Windows 11 ; sans effet avant).
        public static void Sombre(Form f)
        {
            try { var v = 1; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
        }

        // Ce qu'ont en commun toutes les fenêtres secondaires.
        public static void Dialogue(Form f, string titre)
        {
            f.Text = titre;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = false;
            f.StartPosition = FormStartPosition.CenterParent;
            f.BackColor = Nuit;
            f.ForeColor = Texte;
            f.Font = new Font("Segoe UI", 9.5f);
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        }

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

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static void StyleButton(Button b, bool primary)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = primary ? Or : Bordure;
            b.BackColor = primary ? Or : Panneau;
            b.ForeColor = primary ? Encre : Texte;
            b.Cursor = Cursors.Hand;
            if (primary) b.Font = new Font(b.Font, FontStyle.Bold);
        }
    }

    // ----------------------------------------------------------- bouton
    // Coins arrondis (8 px), peint à la main. Icone = nom d'une icône
    // (cf. Icons) : seule si Text est vide, sinon à gauche du texte.
    public class RoundedButton : Button
    {
        private bool _hover;
        public string Icone;

        public RoundedButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Désactivé : le gris clair classique, texte anthracite — quelle que
            // soit la couleur du bouton (l'or grisé restait de l'or, illisible).
            var fill = BackColor;
            if (!Enabled) fill = Color.FromArgb(200, 203, 210);
            else if (_hover) fill = Theme.Mix(fill, Color.White, 0.10f);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                var border = Enabled ? FlatAppearance.BorderColor : Color.FromArgb(168, 172, 182);
                using (var p = new Pen(border)) g.DrawPath(p, path);
            }

            var tc = Enabled ? ForeColor : Color.FromArgb(58, 61, 70);
            var avecIcone = Icons.Existe(Icone);
            var c = Math.Min(Height - 10, 18);
            if (avecIcone && string.IsNullOrEmpty(Text))
            {
                Icons.Dessiner(g, Icone, new Rectangle((Width - c) / 2, (Height - c) / 2, c, c), tc);
                return;
            }
            var zone = ClientRectangle;
            if (avecIcone)
            {
                var lTexte = TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFormatFlags.NoPadding).Width;
                var total = c + 8 + lTexte;
                var x = Math.Max(10, (Width - total) / 2);
                Icons.Dessiner(g, Icone, new Rectangle(x, (Height - c) / 2, c, c), tc);
                zone = new Rectangle(x + c + 8, 0, Width - (x + c + 8), Height);
                TextRenderer.DrawText(g, Text, Font, zone, tc,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding);
                return;
            }
            TextRenderer.DrawText(g, Text, Font, zone, tc,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // ------------------------------------------------------------ champ
    // Un TextBox sans bordure posé dans un cadre arrondi : la bordure
    // passe à l'or quand le champ a le focus. Text, TextChanged, Clear,
    // SelectAll, Focus, Multiline, ReadOnly, ScrollBars, WordWrap se
    // manipulent comme sur un TextBox ordinaire ; le TextBox lui-même
    // reste accessible (TextBox) pour le reste.
    public class RoundedField : Control
    {
        private readonly TextBox _tb = new TextBox();

        public RoundedField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 28;
            Cursor = Cursors.IBeam;
            _tb.BorderStyle = BorderStyle.None;
            _tb.BackColor = Theme.Panneau;
            _tb.ForeColor = Theme.Texte;
            _tb.GotFocus += delegate { Invalidate(); };
            _tb.LostFocus += delegate { Invalidate(); };
            _tb.TextChanged += delegate { OnTextChanged(EventArgs.Empty); };
            _tb.KeyDown += delegate(object s, KeyEventArgs e) { OnKeyDown(e); };
            Controls.Add(_tb);
            Placer();
        }

        public TextBox TextBox { get { return _tb; } }

        public override string Text
        {
            get { return _tb.Text; }
            set { _tb.Text = value; }
        }

        public bool Multiline
        {
            get { return _tb.Multiline; }
            set { _tb.Multiline = value; Placer(); }
        }
        public bool ReadOnly { get { return _tb.ReadOnly; } set { _tb.ReadOnly = value; } }
        public bool WordWrap { get { return _tb.WordWrap; } set { _tb.WordWrap = value; } }
        public ScrollBars ScrollBars { get { return _tb.ScrollBars; } set { _tb.ScrollBars = value; } }
        public int SelectionStart { get { return _tb.SelectionStart; } set { _tb.SelectionStart = value; } }
        public int SelectionLength { get { return _tb.SelectionLength; } set { _tb.SelectionLength = value; } }

        public void Clear() { _tb.Clear(); }
        public void SelectAll() { _tb.SelectAll(); }
        public new bool Focus() { return _tb.Focus(); }
        public override bool Focused { get { return _tb.Focused; } }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _tb.Font = Font;
            Placer();
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            _tb.ForeColor = ForeColor;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _tb.Enabled = Enabled;
            _tb.BackColor = Enabled ? Theme.Panneau : Theme.Mix(Theme.Panneau, Theme.Nuit, 0.5f);
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Placer(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _tb.Focus(); }

        private void Placer()
        {
            if (_tb.Multiline)
                _tb.SetBounds(9, 7, Math.Max(10, Width - 18), Math.Max(10, Height - 14));
            else
            {
                var h = _tb.PreferredHeight;
                _tb.SetBounds(10, Math.Max(2, (Height - h) / 2), Math.Max(10, Width - 20), h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Nuit);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(_tb.BackColor)) g.FillPath(b, path);
                using (var p = new Pen(_tb.Focused ? Theme.Or : Theme.Bordure)) g.DrawPath(p, path);
            }
        }
    }

    // ------------------------------------------------------------ liste
    // Une ListBox sans bordure dans le même cadre arrondi : Items se
    // manipule comme sur une ListBox ordinaire.
    public class RoundedList : Control
    {
        private readonly ListBox _lb = new ListBox();

        public RoundedList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _lb.BorderStyle = BorderStyle.None;
            _lb.BackColor = Theme.Panneau;
            _lb.ForeColor = Theme.Texte;
            _lb.IntegralHeight = false;
            _lb.HorizontalScrollbar = true;
            _lb.GotFocus += delegate { Invalidate(); };
            _lb.LostFocus += delegate { Invalidate(); };
            Controls.Add(_lb);
            Placer();
        }

        public ListBox.ObjectCollection Items { get { return _lb.Items; } }
        public ListBox ListBox { get { return _lb; } }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); _lb.Font = Font; Placer(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Placer(); }

        private void Placer()
        {
            _lb.SetBounds(9, 6, Math.Max(10, Width - 18), Math.Max(10, Height - 12));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Nuit);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(_lb.BackColor)) g.FillPath(b, path);
                using (var p = new Pen(_lb.Focused ? Theme.Or : Theme.Bordure)) g.DrawPath(p, path);
            }
        }
    }

    // ListBox. Items, SelectedIndex, SelectedItem, SelectedIndexChanged
    // se manipulent comme sur un ComboBox en mode DropDownList.
    public class RoundedCombo : Control
    {
        public readonly List<object> Items = new List<object>();
        public event EventHandler SelectedIndexChanged;
        private int _index = -1;
        private bool _hover;
        private bool _ouvert;

        public RoundedCombo()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            Height = 28;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int SelectedIndex
        {
            get { return _index; }
            set
            {
                if (value < -1 || value >= Items.Count) value = -1;
                if (value == _index) return;
                _index = value;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return _index >= 0 && _index < Items.Count ? Items[_index] : null; }
            set
            {
                for (var i = 0; i < Items.Count; i++)
                    if (Equals(Items[i], value) || (value != null && Items[i].ToString() == value.ToString()))
                    { SelectedIndex = i; return; }
                SelectedIndex = -1;
            }
        }

        public override string Text
        {
            get { var o = SelectedItem; return o == null ? "" : o.ToString(); }
            set { SelectedItem = value; }
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button == MouseButtons.Left && Enabled) Ouvrir();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Enter ||
                keyData == (Keys.Alt | Keys.Down)) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled || Items.Count == 0) return;
            if (e.KeyCode == Keys.Down && e.Alt || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            { Ouvrir(); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { SelectedIndex = Math.Min(Items.Count - 1, _index + 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { SelectedIndex = Math.Max(0, _index - 1); e.Handled = true; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Focused || Items.Count == 0) return;
            if (e.Delta < 0) SelectedIndex = Math.Min(Items.Count - 1, _index + 1);
            else if (e.Delta > 0) SelectedIndex = Math.Max(0, _index - 1);
        }

        private void Ouvrir()
        {
            if (_ouvert || Items.Count == 0) return;
            var liste = new ListBox();
            liste.BorderStyle = BorderStyle.None;
            liste.BackColor = Theme.Panneau;
            liste.ForeColor = Theme.Texte;
            liste.Font = Font;
            liste.IntegralHeight = false;
            liste.DrawMode = DrawMode.OwnerDrawFixed;
            liste.ItemHeight = 26;
            foreach (var o in Items) liste.Items.Add(o);
            liste.SelectedIndex = _index;
            liste.Width = Width - 2;
            liste.Height = Math.Min(Items.Count, 10) * 26;
            liste.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                var choisi = (e.State & DrawItemState.Selected) != 0;
                using (var fond = new SolidBrush(choisi ? Theme.Or : Theme.Panneau))
                    e.Graphics.FillRectangle(fond, e.Bounds);
                TextRenderer.DrawText(e.Graphics, liste.Items[e.Index].ToString(), liste.Font,
                    new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                    choisi ? Theme.Encre : Theme.Texte,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };
            // la sélection suit la souris, comme dans un menu
            liste.MouseMove += delegate(object s, MouseEventArgs e)
            {
                var i = liste.IndexFromPoint(e.Location);
                if (i >= 0 && i != liste.SelectedIndex) liste.SelectedIndex = i;
            };

            var menu = new ToolStripDropDown();
            menu.AutoSize = false;
            menu.Padding = Padding.Empty;
            menu.Margin = Padding.Empty;
            menu.BackColor = Theme.Panneau;
            menu.Renderer = new ToolStripProfessionalRenderer(new NuitCouleurs());
            var hote = new ToolStripControlHost(liste);
            hote.Padding = Padding.Empty;
            hote.Margin = Padding.Empty;
            hote.AutoSize = false;
            hote.Size = liste.Size;
            menu.Items.Add(hote);
            menu.Size = new Size(liste.Width + 2, liste.Height + 2);
            EventHandler choisir = delegate
            {
                var i = liste.SelectedIndex;
                menu.Close();
                if (i >= 0) SelectedIndex = i;
            };
            liste.Click += choisir;
            liste.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { choisir(s, e); e.Handled = true; }
            };
            menu.Closed += delegate { _ouvert = false; Invalidate(); };
            _ouvert = true;
            Invalidate();
            menu.Show(this, new Point(0, Height + 2));
            liste.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Nuit);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var fond = Enabled ? Theme.Panneau : Theme.Mix(Theme.Panneau, Theme.Nuit, 0.5f);
            if (Enabled && _hover) fond = Theme.Mix(fond, Color.White, 0.06f);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.RoundedRect(rect, 8))
            {
                using (var b = new SolidBrush(fond)) g.FillPath(b, path);
                using (var p = new Pen(Focused || _ouvert ? Theme.Or : Theme.Bordure)) g.DrawPath(p, path);
            }
            var encre = Enabled ? Theme.Texte : Color.FromArgb(120, 126, 150);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 36, Height), encre,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
            // le chevron
            var cx = Width - 16;
            var cy = Height / 2;
            using (var p = new Pen(Enabled ? Theme.TexteDoux : encre, 1.8f))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 5, cy - 2, cx, cy + 3);
                g.DrawLine(p, cx, cy + 3, cx + 5, cy - 2);
            }
        }
    }


    // ------------------------------------------------------------- menus
    // Palette sombre des menus : WinForms les rend blancs.
    public class NuitCouleurs : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Theme.Nuit; } }
        public override Color MenuStripGradientEnd { get { return Theme.Nuit; } }
        public override Color MenuItemSelected { get { return Theme.Bordure; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Bordure; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Bordure; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Panneau; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Panneau; } }
        public override Color MenuItemBorder { get { return Theme.Bordure; } }
        public override Color MenuBorder { get { return Theme.Bordure; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Panneau; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panneau; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panneau; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panneau; } }
        public override Color SeparatorDark { get { return Theme.Bordure; } }
        public override Color SeparatorLight { get { return Theme.Bordure; } }
    }

    public static class Menus
    {
        public static void Styler(MenuStrip m)
        {
            m.Renderer = new ToolStripProfessionalRenderer(new NuitCouleurs());
            m.BackColor = Theme.Nuit;
            m.ForeColor = Theme.Texte;
            m.Font = new Font("Segoe UI", 9.5f);
            m.Padding = new Padding(8, 4, 0, 2);
            foreach (ToolStripMenuItem it in m.Items) Colorer(it);
        }

        private static void Colorer(ToolStripMenuItem it)
        {
            it.ForeColor = Theme.Texte;
            foreach (ToolStripItem s in it.DropDownItems)
            {
                s.ForeColor = Theme.Texte;
                var sm = s as ToolStripMenuItem;
                if (sm != null) Colorer(sm);
            }
        }

        public static ToolStripMenuItem Entree(string texte, Keys raccourci, EventHandler action)
        {
            var it = new ToolStripMenuItem(texte);
            if (raccourci != Keys.None) it.ShortcutKeys = raccourci;
            it.Click += action;
            return it;
        }
    }

    // ---------------------------------------------------------- dialogue
    // Le remplaçant de MessageBox, sur le modèle de Marabook : pastille
    // de sens (rouge = erreur, or = avertissement, bleu = question ou
    // information), message, boutons alignés à droite, le principal en
    // or. Fermer par la croix ou Échap rend le refus le plus sûr. Avec
    // « details », un bouton « Détails techniques » à gauche déplie le
    // journal brut (Consolas, sans retour à la ligne).
    public class MessageDialog : Form
    {
        private DialogResult _resultat;
        private RoundedField _details;
        private RoundedButton _btnDetails;
        private int _hauteurRepliee;

        private MessageDialog(string texte, string titre, MessageBoxButtons boutons,
            MessageBoxIcon icone, string details)
        {
            Theme.Dialogue(this, titre ?? "Skadoosh converter");
            _resultat = Refus(boutons);

            var couleur = Color.Empty;
            var signe = "";
            switch (icone)
            {
                case MessageBoxIcon.Error: couleur = Theme.Erreur; signe = "✕"; break;
                case MessageBoxIcon.Warning: couleur = Theme.Or; signe = "!"; break;
                case MessageBoxIcon.Question: couleur = Theme.Info; signe = "?"; break;
                case MessageBoxIcon.Information: couleur = Theme.Info; signe = "i"; break;
            }
            var avecPastille = signe.Length > 0;
            var xTexte = avecPastille ? 58 : 20;
            var wTexte = 440;
            var mesure = TextRenderer.MeasureText(texte ?? "", Font, new Size(wTexte, 0),
                TextFormatFlags.WordBreak);
            var hTexte = Math.Max(30, mesure.Height + 6);

            if (avecPastille)
            {
                var pastille = new Pastille();
                pastille.Couleur = couleur;
                pastille.Signe = signe;
                pastille.SetBounds(20, 18, 26, 26);
                Controls.Add(pastille);
            }
            var lbl = new Label();
            lbl.Text = texte ?? "";
            lbl.SetBounds(xTexte, 20, wTexte, hTexte);
            Controls.Add(lbl);

            var wClient = xTexte + wTexte + 20;
            var yBoutons = 20 + hTexte + 16;
            var xd = wClient - 20;
            RoundedButton principal = null;
            if (boutons == MessageBoxButtons.OKCancel || boutons == MessageBoxButtons.YesNoCancel)
                xd = Bouton("Annuler", DialogResult.Cancel, false, xd, yBoutons);
            if (boutons == MessageBoxButtons.YesNo || boutons == MessageBoxButtons.YesNoCancel)
            {
                xd = Bouton("Non", DialogResult.No, false, xd, yBoutons);
                principal = BoutonA("Oui", DialogResult.Yes, true, ref xd, yBoutons);
            }
            if (boutons == MessageBoxButtons.OK || boutons == MessageBoxButtons.OKCancel)
                principal = BoutonA("OK", DialogResult.OK, true, ref xd, yBoutons);
            if (principal != null) AcceptButton = principal;

            _hauteurRepliee = yBoutons + 32 + 18;
            if (!string.IsNullOrEmpty(details) && details.Trim().Length > 0)
            {
                _btnDetails = new RoundedButton();
                _btnDetails.Text = "Détails techniques  ▾";
                _btnDetails.SetBounds(20, yBoutons, 190, 32);
                Theme.StyleButton(_btnDetails, false);
                _btnDetails.Click += delegate { Basculer(); };
                Controls.Add(_btnDetails);

                _details = new RoundedField();
                _details.Multiline = true;
                _details.ReadOnly = true;
                _details.WordWrap = false;
                _details.ScrollBars = ScrollBars.Both;
                _details.Font = new Font("Consolas", 8.5f);
                _details.ForeColor = Theme.TexteDoux;
                _details.SetBounds(20, yBoutons + 32 + 12, wClient - 40, 200);
                _details.Text = details;
                _details.Visible = false;
                Controls.Add(_details);
            }
            ClientSize = new Size(wClient, _hauteurRepliee);
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
        }

        private RoundedButton BoutonA(string libelle, DialogResult r, bool primaire, ref int xd, int y)
        {
            var b = new RoundedButton();
            b.Text = libelle;
            xd -= 96;
            b.SetBounds(xd, y, 96, 32);
            xd -= 8;
            Theme.StyleButton(b, primaire);
            b.Click += delegate { _resultat = r; Close(); };
            Controls.Add(b);
            return b;
        }

        private int Bouton(string libelle, DialogResult r, bool primaire, int xd, int y)
        {
            BoutonA(libelle, r, primaire, ref xd, y);
            return xd;
        }

        private void Basculer()
        {
            _details.Visible = !_details.Visible;
            _btnDetails.Text = _details.Visible ? "Détails techniques  ▴" : "Détails techniques  ▾";
            ClientSize = new Size(ClientSize.Width,
                _details.Visible ? _hauteurRepliee + 12 + 200 : _hauteurRepliee);
        }

        private static DialogResult Refus(MessageBoxButtons boutons)
        {
            switch (boutons)
            {
                case MessageBoxButtons.OK: return DialogResult.OK;
                case MessageBoxButtons.YesNo: return DialogResult.No;
                default: return DialogResult.Cancel;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.Sombre(this);
        }

        public static DialogResult Show(IWin32Window owner, string texte, string titre,
            MessageBoxButtons boutons, MessageBoxIcon icone)
        {
            return Show(owner, texte, titre, boutons, icone, null);
        }

        public static DialogResult Show(IWin32Window owner, string texte, string titre,
            MessageBoxButtons boutons, MessageBoxIcon icone, string details)
        {
            using (var d = new MessageDialog(texte, titre, boutons, icone, details))
            {
                var f = owner as Form;
                if (f != null && f.Visible)
                {
                    d.StartPosition = FormStartPosition.CenterParent;
                    if (f.Icon != null) d.Icon = f.Icon;
                    d.ShowDialog(owner);
                }
                else
                {
                    d.StartPosition = FormStartPosition.CenterScreen;
                    d.ShowDialog();
                }
                return d._resultat;
            }
        }

        // Une erreur avec sa cause technique repliée.
        public static void Erreur(IWin32Window owner, string texte, Exception ex)
        {
            Show(owner, texte, "Skadoosh converter", MessageBoxButtons.OK, MessageBoxIcon.Error,
                ex == null ? null : ex.ToString());
        }

        // Le rond coloré avec son signe.
        private class Pastille : Control
        {
            public Color Couleur;
            public string Signe = "";

            public Pastille()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(Parent != null ? Parent.BackColor : Theme.Nuit);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(Couleur)) g.FillEllipse(b, 0, 0, Width - 1, Height - 1);
                using (var f = new Font("Segoe UI", 11f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Signe, f, ClientRectangle, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}
