#!/bin/zsh
# deps.sh — les dépendances tierces de Stargazer pour macOS.
# Équivalent des install-*.ps1 de la version Windows : tout se télécharge
# depuis les sources officielles au premier besoin, rien n'est installé
# dans le système — tout vit dans le dossier de dépendances partagé (voir
# DEPS ci-dessous), commun à toutes les apps de la famille Stargazer.
#
#   deps.sh ensure <outil>...   installe si absent (ffmpeg ffprobe yt-dlp
#                               gallery-dl deno pandoc)
#   deps.sh where <outil>       affiche le chemin (vide si absent)
#   deps.sh present <outil>     code retour 0 si présent
#
# Sources (cf. READMEs officiels de chaque projet) :
#   yt-dlp     binaire macOS officiel (GitHub yt-dlp)
#   ffmpeg     builds statiques macOS liés par ffmpeg.org
#              (evermeet.cx en x86_64, martin-riedl.de en arm64)
#   pandoc     zip macOS officiel (GitHub jgm/pandoc)
#   deno       zip apple-darwin officiel (GitHub denoland)
#   gallery-dl pas de binaire mac officiel : on embarque un CPython
#              portable (astral-sh/python-build-standalone) dans
#              dependencies/ et on y installe gallery-dl via pip —
#              le système n'est jamais touché.
set -e

SCRIPT_DIR="${0:a:h}"
# Dossier des dépendances, PARTAGÉ par toutes les apps de la famille
# Stargazer (un moteur n'est jamais téléchargé deux fois) : STARGAZER_DEPS
# si l'app hôte le donne, sinon <racine mac du hub>/dependencies quand on
# vit dans Stargazer (dossier marqué .stargazer-racine), sinon
# ~/Library/Application Support/Stargazer/dependencies (app autonome).
if [[ -n "${STARGAZER_DEPS:-}" ]]; then
  DEPS="$STARGAZER_DEPS"
else
  DEPS="$HOME/Library/Application Support/Stargazer/dependencies"
  d="$SCRIPT_DIR"
  for i in {1..8}; do
    if [[ -f "$d/.stargazer-racine" ]]; then DEPS="$d/dependencies"; break; fi
    [[ "${d:h}" == "$d" ]] && break
    d="${d:h}"
  done
fi
BIN="$DEPS/bin"
ARCH="$(uname -m)"    # arm64 ou x86_64
mkdir -p "$BIN"

say() { print -r -- "[deps] $*"; }

telecharger() {  # url, destination
  say "téléchargement : $1"
  curl -fL --retry 3 --connect-timeout 20 -o "$2" "$1"
}

github_asset_url() {  # repo, motif (grep -E) -> url du premier asset
  curl -fsL --retry 3 "https://api.github.com/repos/$1/releases/latest" |
    grep -oE '"browser_download_url": *"[^"]+"' |
    grep -oE 'https://[^"]+' |
    grep -E "$2" | head -1
}

sans_quarantaine() { xattr -dr com.apple.quarantine "$1" 2>/dev/null || true; }

# ---------------------------------------------------------------- outils

installer_yt_dlp() {
  # Canal NIGHTLY (depuis 2.12.0) : YouTube casse les téléchargements
  # toutes les quelques semaines et les correctifs arrivent dans les
  # builds nightly bien avant la stable suivante (consigne officielle
  # yt-dlp pour les « HTTP Error 403 » et compagnie).
  telecharger "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp_macos" \
    "$BIN/yt-dlp"
  chmod +x "$BIN/yt-dlp"; sans_quarantaine "$BIN/yt-dlp"
}

installer_ffmpeg_un() {  # ffmpeg ou ffprobe
  local outil="$1" tmp url
  tmp="$(mktemp -d -t stargazer-ffmpeg)"
  if [[ "$ARCH" == "arm64" ]]; then
    # martin-riedl.de (lié par ffmpeg.org) : on repère le dernier build
    # arm64 sur la page d'accueil
    url="https://ffmpeg.martin-riedl.de$(curl -fsL --retry 3 \
      'https://ffmpeg.martin-riedl.de/' |
      grep -oE 'href="/download/macos/arm64/[^"]*/'"$outil"'\.zip"' |
      head -1 | sed -E 's/^href="|"$//g')"
    [[ "$url" != "https://ffmpeg.martin-riedl.de" ]] ||
      { say "ERREUR : build $outil arm64 introuvable"; return 1; }
  else
    url="https://evermeet.cx/ffmpeg/getrelease/$outil/zip"
  fi
  telecharger "$url" "$tmp/$outil.zip"
  ditto -x -k "$tmp/$outil.zip" "$tmp/ext"
  local bin_trouve
  bin_trouve="$(find "$tmp/ext" -type f -name "$outil" | head -1)"
  [[ -n "$bin_trouve" ]] || { say "ERREUR : $outil introuvable dans le zip"; return 1; }
  mv "$bin_trouve" "$BIN/$outil"
  chmod +x "$BIN/$outil"; sans_quarantaine "$BIN/$outil"
  rm -rf "$tmp"
}

installer_pandoc() {
  local motif tmp url
  [[ "$ARCH" == "arm64" ]] && motif='arm64-macOS\.zip' || motif='x86_64-macOS\.zip'
  url="$(github_asset_url jgm/pandoc "$motif")"
  [[ -n "$url" ]] || { say "ERREUR : release pandoc introuvable"; return 1; }
  tmp="$(mktemp -d -t stargazer-pandoc)"
  telecharger "$url" "$tmp/pandoc.zip"
  ditto -x -k "$tmp/pandoc.zip" "$tmp/ext"
  local bin_trouve
  bin_trouve="$(find "$tmp/ext" -type f -name pandoc | head -1)"
  [[ -n "$bin_trouve" ]] || { say "ERREUR : binaire pandoc introuvable"; return 1; }
  mv "$bin_trouve" "$BIN/pandoc"
  chmod +x "$BIN/pandoc"; sans_quarantaine "$BIN/pandoc"
  rm -rf "$tmp"
}

installer_deno() {
  local nom tmp
  [[ "$ARCH" == "arm64" ]] && nom="deno-aarch64-apple-darwin.zip" \
                           || nom="deno-x86_64-apple-darwin.zip"
  tmp="$(mktemp -d -t stargazer-deno)"
  telecharger "https://github.com/denoland/deno/releases/latest/download/$nom" \
    "$tmp/deno.zip"
  ditto -x -k "$tmp/deno.zip" "$tmp/ext"
  mv "$tmp/ext/deno" "$BIN/deno"
  chmod +x "$BIN/deno"; sans_quarantaine "$BIN/deno"
  rm -rf "$tmp"
}

installer_python_portable() {
  [[ -x "$DEPS/python/python/bin/python3" ]] && return 0
  local motif url tmp
  [[ "$ARCH" == "arm64" ]] && motif='aarch64-apple-darwin-install_only\.tar\.gz$' \
                           || motif='x86_64-apple-darwin-install_only\.tar\.gz$'
  url="$(github_asset_url astral-sh/python-build-standalone "$motif")"
  [[ -n "$url" ]] || { say "ERREUR : CPython portable introuvable"; return 1; }
  tmp="$(mktemp -d -t stargazer-python)"
  telecharger "$url" "$tmp/python.tar.gz"
  say "déballage du Python portable…"
  mkdir -p "$DEPS/python"
  tar -xzf "$tmp/python.tar.gz" -C "$DEPS/python"
  sans_quarantaine "$DEPS/python"
  rm -rf "$tmp"
}

installer_gallery_dl() {
  installer_python_portable
  say "pip install gallery-dl (dans le Python portable, pas le système)…"
  "$DEPS/python/python/bin/python3" -m pip install --quiet --upgrade \
    --no-warn-script-location gallery-dl
  # lanceur relatif : le dossier dependencies/ peut voyager (clé USB…)
  cat > "$BIN/gallery-dl" <<'FIN'
#!/bin/zsh
DIR="${0:a:h}"
exec "$DIR/../python/python/bin/python3" -m gallery_dl "$@"
FIN
  chmod +x "$BIN/gallery-dl"
}

# ------------------------------------------------------------- commandes

chemin_de() {
  local outil="$1"
  if [[ -x "$BIN/$outil" ]]; then print -r -- "$BIN/$outil"; return 0; fi
  local dans_path
  dans_path="$(command -v "$outil" 2>/dev/null || true)"
  [[ -n "$dans_path" ]] && { print -r -- "$dans_path"; return 0; }
  return 1
}

ensure_un() {
  local outil="$1"
  if chemin_de "$outil" >/dev/null; then say "$outil : déjà là."; return 0; fi
  say "$outil absent : installation…"
  case "$outil" in
    yt-dlp)     installer_yt_dlp ;;
    ffmpeg)     installer_ffmpeg_un ffmpeg ;;
    ffprobe)    installer_ffmpeg_un ffprobe ;;
    pandoc)     installer_pandoc ;;
    deno)       installer_deno ;;
    gallery-dl) installer_gallery_dl ;;
    *) say "outil inconnu : $outil"; return 1 ;;
  esac
  say "$outil : installé -> $BIN/$outil"
}

case "${1:-}" in
  ensure)  shift; for o in "$@"; do ensure_un "$o"; done ;;
  where)   chemin_de "${2:?outil ?}" || true ;;
  present) chemin_de "${2:?outil ?}" >/dev/null ;;
  *) say "usage : deps.sh ensure|where|present <outil>"; exit 2 ;;
esac
