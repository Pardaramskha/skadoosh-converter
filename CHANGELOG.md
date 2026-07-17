# Changelog — Skadoosh converter

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
