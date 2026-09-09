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
    public static class Program
    {
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
