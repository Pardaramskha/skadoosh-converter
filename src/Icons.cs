using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

// Icons.cs — les icônes des boutons : tracés Phosphor « bold » (viewBox
// 256) embarqués tels quels et rendus en GDI+ à la couleur du texte, les
// mêmes que dans Markdown we go, Marabook et My Somehow Legal Downloader.
// SvgTrace lit le « d » d'un tracé SVG vers un GraphicsPath.

namespace SkadooshConverter
{
    public static class Icons
    {
        private static readonly Dictionary<string, string> Traces = new Dictionary<string, string>();
        private static readonly Dictionary<string, GraphicsPath> Cache = new Dictionary<string, GraphicsPath>();

        static Icons()
        {
            // folder-open
            Traces["ouvrir"] = "M216,68H133.39l-26-29.29a20,20,0,0,0-15-6.71H40A20,20,0,0,0,20,52V200.62A19.41,19.41,0,0,0,39.38,220H216.89A19.13,19.13,0,0,0,236,200.89V88A20,20,0,0,0,216,68ZM44,56H90.61l10.67,12H44ZM212,196H44V92H212Z";
            // clipboard
            Traces["coller"] = "M216,28H88A12,12,0,0,0,76,40V76H40A12,12,0,0,0,28,88V216a12,12,0,0,0,12,12H168a12,12,0,0,0,12-12V180h36a12,12,0,0,0,12-12V40A12,12,0,0,0,216,28ZM156,204H52V100H156Zm48-48H180V88a12,12,0,0,0-12-12H100V52H204Z";
            // paragraph (le pied-de-mouche, glyphe du typographe)
            Traces["nettoyer"] = "M208,36H96a68,68,0,0,0,0,136h36v36a12,12,0,0,0,24,0V60h16V208a12,12,0,0,0,24,0V60h12a12,12,0,0,0,0-24ZM132,148H96a44,44,0,0,1,0-88h36Z";
            // eye
            Traces["apercu"] = "M251,123.13c-.37-.81-9.13-20.26-28.48-39.61C196.63,57.67,164,44,128,44S59.37,57.67,33.51,83.52C14.16,102.87,5.4,122.32,5,123.13a12.08,12.08,0,0,0,0,9.75c.37.82,9.13,20.26,28.49,39.61C59.37,198.34,92,212,128,212s68.63-13.66,94.48-39.51c19.36-19.35,28.12-38.79,28.49-39.61A12.08,12.08,0,0,0,251,123.13Zm-46.06,33C183.47,177.27,157.59,188,128,188s-55.47-10.73-76.91-31.88A130.36,130.36,0,0,1,29.52,128,130.45,130.45,0,0,1,51.09,99.89C72.54,78.73,98.41,68,128,68s55.46,10.73,76.91,31.89A130.36,130.36,0,0,1,226.48,128,130.45,130.45,0,0,1,204.91,156.12ZM128,84a44,44,0,1,0,44,44A44.05,44.05,0,0,0,128,84Zm0,64a20,20,0,1,1,20-20A20,20,0,0,1,128,148Z";
            // copy-simple
            Traces["copier"] = "M180,64H40A12,12,0,0,0,28,76V216a12,12,0,0,0,12,12H180a12,12,0,0,0,12-12V76A12,12,0,0,0,180,64ZM168,204H52V88H168ZM228,40V180a12,12,0,0,1-24,0V52H76a12,12,0,0,1,0-24H216A12,12,0,0,1,228,40Z";
            // file-arrow-down
            Traces["enregistrer"] = "M216.49,79.52l-56-56A12,12,0,0,0,152,20H56A20,20,0,0,0,36,40V216a20,20,0,0,0,20,20H200a20,20,0,0,0,20-20V88A12,12,0,0,0,216.49,79.52ZM160,57l23,23H160ZM60,212V44h76V92a12,12,0,0,0,12,12h48V212Zm100.49-60.49a12,12,0,0,1,0,17l-24,24a12,12,0,0,1-17,0l-24-24a12,12,0,0,1,17-17L116,155V124a12,12,0,0,1,24,0v31l3.51-3.52A12,12,0,0,1,160.49,151.51Z";
            // check-square
            Traces["regles"] = "M79.51,144.49a12,12,0,1,1,17-17L112,143l47.51-47.52a12,12,0,0,1,17,17l-56,56a12,12,0,0,1-17,0ZM228,48V208a20,20,0,0,1-20,20H48a20,20,0,0,1-20-20V48A20,20,0,0,1,48,28H208A20,20,0,0,1,228,48Zm-24,4H52V204H204Z";
            // trash
            Traces["corbeille"] = "M216,48H180V36A28,28,0,0,0,152,8H104A28,28,0,0,0,76,36V48H40a12,12,0,0,0,0,24h4V208a20,20,0,0,0,20,20H192a20,20,0,0,0,20-20V72h4a12,12,0,0,0,0-24ZM100,36a4,4,0,0,1,4-4h48a4,4,0,0,1,4,4V48H100Zm88,168H68V72H188ZM116,104v64a12,12,0,0,1-24,0V104a12,12,0,0,1,24,0Zm48,0v64a12,12,0,0,1-24,0V104a12,12,0,0,1,24,0Z";
        }

        public static bool Existe(string nom)
        {
            return nom != null && Traces.ContainsKey(nom);
        }

        // Dessine l'icône dans le rectangle (proportions gardées).
        public static void Dessiner(Graphics g, string nom, Rectangle r, Color couleur)
        {
            string trace;
            if (nom == null || !Traces.TryGetValue(nom, out trace)) return;
            GraphicsPath chemin;
            if (!Cache.TryGetValue(nom, out chemin))
            {
                chemin = SvgTrace.Lire(trace);
                Cache[nom] = chemin;
            }
            var etat = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            g.ScaleTransform(r.Width / 256f, r.Height / 256f);
            using (var b = new SolidBrush(couleur)) g.FillPath(b, chemin);
            g.Restore(etat);
        }
    }

    // Lecteur du « d » d'un tracé SVG vers un GraphicsPath : M L H V C S
    // Q T A Z, absolus et relatifs, nombres collés et drapeaux d'arc
    // collés. Les arcs passent par la paramétrisation centrale.
    public static class SvgTrace
    {
        private class Lecteur
        {
            private readonly string _s;
            private int _i;
            public Lecteur(string s) { _s = s; }

            private void Sauter()
            {
                while (_i < _s.Length && (char.IsWhiteSpace(_s[_i]) || _s[_i] == ',')) _i++;
            }

            public bool Reste { get { Sauter(); return _i < _s.Length; } }

            public bool Commande()
            {
                Sauter();
                return _i < _s.Length && char.IsLetter(_s[_i]) && _s[_i] != 'e' && _s[_i] != 'E';
            }

            public char LireCommande() { Sauter(); return _s[_i++]; }

            public bool Nombre(out float v)
            {
                Sauter();
                var debut = _i;
                if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                if (_i < _s.Length && _s[_i] == '.')
                {
                    _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    var sauve = _i;
                    _i++;
                    if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    if (_i < _s.Length && char.IsDigit(_s[_i]))
                        while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                    else _i = sauve;
                }
                if (_i == debut) { v = 0; return false; }
                return float.TryParse(_s.Substring(debut, _i - debut), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out v);
            }

            public bool Drapeau(out bool f)
            {
                Sauter();
                f = false;
                if (_i >= _s.Length) return false;
                if (_s[_i] == '0') { _i++; return true; }
                if (_s[_i] == '1') { _i++; f = true; return true; }
                return false;
            }
        }

        public static GraphicsPath Lire(string d)
        {
            var chemin = new GraphicsPath(FillMode.Winding);
            var l = new Lecteur(d);
            var cur = PointF.Empty;
            var depart = PointF.Empty;
            var derC = PointF.Empty;
            var derQ = PointF.Empty;
            var cmd = ' ';
            var precedente = ' ';
            try
            {
                while (l.Reste)
                {
                    if (l.Commande()) cmd = l.LireCommande();
                    else if (cmd == 'M') cmd = 'L';
                    else if (cmd == 'm') cmd = 'l';
                    else if (cmd == ' ') break;
                    var rel = char.IsLower(cmd);
                    var maj = char.ToUpperInvariant(cmd);
                    float x, y, x1, y1, x2, y2;
                    switch (maj)
                    {
                        case 'M':
                            if (!l.Nombre(out x) || !l.Nombre(out y)) return chemin;
                            cur = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                            depart = cur;
                            chemin.StartFigure();
                            break;
                        case 'L':
                            if (!l.Nombre(out x) || !l.Nombre(out y)) return chemin;
                            {
                                var p = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                                chemin.AddLine(cur, p);
                                cur = p;
                            }
                            break;
                        case 'H':
                            if (!l.Nombre(out x)) return chemin;
                            {
                                var p = new PointF(rel ? cur.X + x : x, cur.Y);
                                chemin.AddLine(cur, p);
                                cur = p;
                            }
                            break;
                        case 'V':
                            if (!l.Nombre(out y)) return chemin;
                            {
                                var p = new PointF(cur.X, rel ? cur.Y + y : y);
                                chemin.AddLine(cur, p);
                                cur = p;
                            }
                            break;
                        case 'C':
                            if (!l.Nombre(out x1) || !l.Nombre(out y1) || !l.Nombre(out x2) ||
                                !l.Nombre(out y2) || !l.Nombre(out x) || !l.Nombre(out y)) return chemin;
                            {
                                var p1 = rel ? new PointF(cur.X + x1, cur.Y + y1) : new PointF(x1, y1);
                                var p2 = rel ? new PointF(cur.X + x2, cur.Y + y2) : new PointF(x2, y2);
                                var p = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                                chemin.AddBezier(cur, p1, p2, p);
                                derC = p2;
                                cur = p;
                            }
                            break;
                        case 'S':
                            if (!l.Nombre(out x2) || !l.Nombre(out y2) || !l.Nombre(out x) ||
                                !l.Nombre(out y)) return chemin;
                            {
                                var pu = char.ToUpperInvariant(precedente);
                                var p1 = pu == 'C' || pu == 'S'
                                    ? new PointF(2 * cur.X - derC.X, 2 * cur.Y - derC.Y) : cur;
                                var p2 = rel ? new PointF(cur.X + x2, cur.Y + y2) : new PointF(x2, y2);
                                var p = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                                chemin.AddBezier(cur, p1, p2, p);
                                derC = p2;
                                cur = p;
                            }
                            break;
                        case 'Q':
                        case 'T':
                            {
                                PointF p1;
                                if (maj == 'Q')
                                {
                                    if (!l.Nombre(out x1) || !l.Nombre(out y1)) return chemin;
                                    p1 = rel ? new PointF(cur.X + x1, cur.Y + y1) : new PointF(x1, y1);
                                }
                                else
                                {
                                    var pu = char.ToUpperInvariant(precedente);
                                    p1 = pu == 'Q' || pu == 'T'
                                        ? new PointF(2 * cur.X - derQ.X, 2 * cur.Y - derQ.Y) : cur;
                                }
                                if (!l.Nombre(out x) || !l.Nombre(out y)) return chemin;
                                var p = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                                var c1 = new PointF(cur.X + 2f / 3 * (p1.X - cur.X), cur.Y + 2f / 3 * (p1.Y - cur.Y));
                                var c2 = new PointF(p.X + 2f / 3 * (p1.X - p.X), p.Y + 2f / 3 * (p1.Y - p.Y));
                                chemin.AddBezier(cur, c1, c2, p);
                                derQ = p1;
                                cur = p;
                            }
                            break;
                        case 'A':
                            {
                                float rx, ry, rot;
                                bool grand, sens;
                                if (!l.Nombre(out rx) || !l.Nombre(out ry) || !l.Nombre(out rot) ||
                                    !l.Drapeau(out grand) || !l.Drapeau(out sens) ||
                                    !l.Nombre(out x) || !l.Nombre(out y)) return chemin;
                                var p = rel ? new PointF(cur.X + x, cur.Y + y) : new PointF(x, y);
                                Arc(chemin, cur, p, Math.Abs(rx), Math.Abs(ry), grand, sens);
                                cur = p;
                            }
                            break;
                        case 'Z':
                            chemin.CloseFigure();
                            cur = depart;
                            break;
                        default:
                            return chemin;
                    }
                    precedente = cmd;
                }
            }
            catch { }
            return chemin;
        }

        private static void Arc(GraphicsPath chemin, PointF a, PointF b, double rx, double ry,
            bool grand, bool sens)
        {
            if (rx < 1e-6 || ry < 1e-6 || (a.X == b.X && a.Y == b.Y))
            {
                chemin.AddLine(a, b);
                return;
            }
            var dx = (a.X - b.X) / 2.0;
            var dy = (a.Y - b.Y) / 2.0;
            var lambda = dx * dx / (rx * rx) + dy * dy / (ry * ry);
            if (lambda > 1) { rx *= Math.Sqrt(lambda); ry *= Math.Sqrt(lambda); }
            var num = rx * rx * ry * ry - rx * rx * dy * dy - ry * ry * dx * dx;
            var den = rx * rx * dy * dy + ry * ry * dx * dx;
            var coef = (grand != sens ? 1 : -1) * Math.Sqrt(Math.Max(0, num / den));
            var cxp = coef * rx * dy / ry;
            var cyp = coef * -ry * dx / rx;
            var cx = cxp + (a.X + b.X) / 2.0;
            var cy = cyp + (a.Y + b.Y) / 2.0;
            var ux = (dx - cxp) / rx; var uy = (dy - cyp) / ry;
            var vx = (-dx - cxp) / rx; var vy = (-dy - cyp) / ry;
            var debut = Angle(1, 0, ux, uy);
            var balayage = Angle(ux, uy, vx, vy);
            if (!sens && balayage > 0) balayage -= 360;
            else if (sens && balayage < 0) balayage += 360;
            chemin.AddArc((float)(cx - rx), (float)(cy - ry), (float)(2 * rx), (float)(2 * ry),
                (float)debut, (float)balayage);
        }

        private static double Angle(double ux, double uy, double vx, double vy)
        {
            var dot = ux * vx + uy * vy;
            var len = Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy));
            var cos = Math.Max(-1, Math.Min(1, dot / len));
            var ang = Math.Acos(cos) * 180 / Math.PI;
            return ux * vy - uy * vx < 0 ? -ang : ang;
        }
    }
}
