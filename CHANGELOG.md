# C# Changelog — Skadoosh converter

## 1.7.0 — 2026-09-09

- **Skadoosh converter prend son indépendance.** L'application quitte le
  dépôt du hub Stargazer pour vivre dans le sien
  (github.com/Pardaramskha/skadoosh-converter), d'où le hub la propose
  sous « Applications disponibles ». Le dépôt se range : `src/` (un
  fichier par classe), `tools/` (build.bat, release.ps1, setup-stub.cs,
  make-icon.ps1), `assets/`, `scripts/`, et
  `skadoosh-converter.stargazer.json` remplace `manifest.json`. L'édition
  macOS vit sous `mac/` avec son propre hôte (`mac/lib/host.js`) et son
  `deps.sh` — elle ne dépend plus du dossier `mac/` du hub.
- **Release installable.** `tools/release.ps1` fabrique l'archive portable
  Windows (celle que le hub télécharge), l'installeur autonome
  `SkadooshConverter-Setup` (%LOCALAPPDATA%\Programs, raccourcis,
  désinstallation depuis Paramètres, aucun droit admin), l'archive des
  sources mac et l'auto-installeur `.command`.
- **Mises à jour depuis l'application.** Barre de menus (Fichier, Aide) et
  « Aide > Vérifier les mises à jour… » : la dernière release GitHub,
  ses notes, et l'installation sur place avec redémarrage. Au lancement,
  une vérification silencieuse : s'il y a plus récent, le menu Aide porte
  un point. Le standard des apps de la famille Stargazer.
- **Les moteurs dans un dossier partagé.** Hors du hub, FFmpeg, ImageMagick
  et Pandoc vont dans `%LOCALAPPDATA%\Stargazer\dependencies` (mac :
  `~/Library/Application Support/Stargazer/dependencies`), commun à toutes
  les apps de la famille — jamais téléchargés deux fois. Dans le hub,
  toujours `<hub>\dependencies` ; `STARGAZER_DEPS` prime. Plus de `bin\`
  local.
- **L'habillage rejoint la norme de la famille** : boutons arrondis à
  icônes, liste des fichiers et liste des formats dans des cadres
  arrondis, barre de titre sombre sur toutes les fenêtres, boîte de
  dialogue maison (pastille de sens, détails techniques repliés) à la
  place des MessageBox.

## 1.6.0 — 2026-07-19

- **Les moteurs s'installent tout seuls.** FFmpeg, ImageMagick et Pandoc
  manquants se téléchargent automatiquement à l'ouverture, via une
  petite fenêtre « Premiers préparatifs » (progression par moteur,
  « Continuer en arrière-plan » pour ne pas attendre) — aucune question
  posée, une fois suffit. Le bouton « Installer les dépendances » et
  l'étiquette d'état disparaissent. Dernier filet au moment de
  convertir si un moteur manque encore.

## 1.5.0 — 2026-07-19

- **Nouvelle famille : Textes (Pandoc).** Déposez des `.docx`, `.odt`,
  `.md`, `.rtf`, `.html`, `.epub`, `.txt`, `.tex` ou `.rst` et
  convertissez-les en `docx`, `odt`, `md`, `rtf`, `html`, `epub` ou
  `txt` — moteur **Pandoc**, installé d'un clic comme FFmpeg et
  ImageMagick (`scripts/install-pandoc.ps1`, release GitHub officielle,
  dossier de dépendances partagé sous Stargazer). Le codename
  « Passe-partout » de l'EDITION-ROADMAP vit désormais ici.

## 1.4.0 — 2026-07-19

- **Dossier de dépendances partagé.** Sous une installation Stargazer
  complète, FFmpeg et ImageMagick s'installent et se cherchent dans
  `<hub>\dependencies\` (partagé entre toutes les apps) ; en autonome,
  `bin\` local comme avant. Recherche : partagé → `bin\` local → PATH.

## 1.3.0 — 2026-07-18

- **Installation d'ImageMagick réparée.** L'index officiel
  `imagemagick.org/archive/binaries/` a disparu (404) et les archives
  portables n'existent plus qu'en `.7z` : le script télécharge désormais
  la dernière release GitHub officielle (`portable-Q16-x64.7z`), vérifie
  son **empreinte SHA-256** (fournie par l'API GitHub) et l'extrait avec
  le `tar.exe` intégré à Windows 10+. Toujours portable, toujours sans
  toucher au système.
- **Fini l'échec muet.** Toute la sortie des scripts d'installation est
  consignée dans `logs\install.log` ; en cas d'échec, un dialogue montre
  les dernières lignes du journal et propose de l'ouvrir (le patron
  « Voir le journal » de la famille).

## 1.2.0 — 2026-07-17

- **Formats modernes via ImageMagick** : HEIC/HEIF (photos iPhone), WebP
  et AVIF acceptés en entrée ; **WebP disponible en cible**. `magick.exe`
  portable, téléchargé dans `bin\` à la demande
  (`scripts\install-magick.ps1`, dernière version portable officielle).
- Le bouton devient « Installer les dépendances » et installe ce qui
  manque (FFmpeg et/ou ImageMagick), toujours caché, toujours en
  arrière-plan.
- Vers ICO et PDF, les sources modernes passent par un PNG intermédiaire
  puis par notre pipeline natif : rendu identique aux autres formats.

## 1.1.0 — 2026-07-17

- La conversion de documents (LibreOffice) est retirée : pas de solution
  correcte sans installer une suite bureautique complète. Skadoosh se
  concentre sur les images (natif) et l'audio (FFmpeg).
- L'installation de FFmpeg se fait désormais **en arrière-plan, sans
  fenêtre de terminal** : le statut s'affiche dans l'appli, le bouton
  disparaît une fois FFmpeg présent.

## 1.0.0 — 2026-07-17

Première version du convertisseur de fichiers.

- Trois familles : **images** (png, jpg, bmp, gif, tiff, ico, pdf),
  **audio** (mp3, wav, flac, ogg, m4a, opus) et **documents**
  (pdf, docx, odt, rtf, txt, html). Lot de plusieurs fichiers accepté,
  tous de la même famille.
- Images : conversion 100 % native (GDI+), y compris vers ICO (entrée PNG
  256 px) et vers PDF (écrivain PDF minimal embarquant le JPEG — aucun
  composant externe).
- Audio : via FFmpeg ; le bouton « Installer les dépendances » le
  télécharge automatiquement dans `bin\` (build portable gyan.dev).
- Documents : via LibreOffice s'il est installé (détection automatique,
  lien d'installation sinon).
- Les fichiers convertis sont créés à côté des originaux, jamais écrasés
  (`nom (1).ext` en cas de collision) ; les fichiers déjà au bon format
  sont ignorés.
- Chemins en argument de l'exe (glisser-déposer) : pré-remplissent la liste.
- Thème Stargazer, barre de titre sombre, icône dessinée par script.
