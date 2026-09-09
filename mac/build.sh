#!/bin/zsh
# build.sh — compile l'édition macOS de SkadooshConverter en
# applet .app cliquable, ici même (mac/SkadooshConverter.app).
# Aucun SDK requis : l'osacompile livré avec macOS suffit.
#
#   lib/host.js    l'hôte partagé de la famille Stargazer (fenêtre Cocoa +
#                  WKWebView + pont JS), copié depuis le hub — l'app est
#                  autoporteuse, elle ne dépend plus de mac/scripts/ du hub
#   src/main.js    la logique de l'app, concaténée après host.js
#   ui/            la page HTML (ui/sg/ : thème et coquille partagés)
#   scripts/       deps.sh, l'installeur de Pandoc (documents .docx/.odt)
#
# Usage : zsh build.sh       (ou ./build.sh une fois rendu exécutable)
set -e
cd "${0:a:h}"

NOM="SkadooshConverter"
ID_APP="skadoosh-converter"

chmod +x scripts/deps.sh 2>/dev/null || true

# host.js + main.js -> un seul script d'applet
TMP_JS="$(mktemp -t skadoosh-converter-build).js"
cat lib/host.js src/main.js > "$TMP_JS"

# -s : applet « stay-open », la vie se passe dans les rappels Cocoa
rm -rf "$NOM.app"
osacompile -l JavaScript -s -o "$NOM.app" "$TMP_JS"
rm -f "$TMP_JS"

# icône : icon.png (sips, livré avec macOS, fabrique l'icns)
if [[ -f icon.png ]]; then
  ICONSET="$(mktemp -d -t skadoosh-converter-icon)/icone.iconset"
  mkdir -p "$ICONSET"
  for taille in 16 32 128 256 512; do
    sips -z $taille $taille icon.png \
      --out "$ICONSET/icon_${taille}x${taille}.png" >/dev/null 2>&1
    sips -z $((taille*2)) $((taille*2)) icon.png \
      --out "$ICONSET/icon_${taille}x${taille}@2x.png" >/dev/null 2>&1
  done
  if iconutil -c icns "$ICONSET" -o "$NOM.app/Contents/Resources/applet.icns" \
      >/dev/null 2>&1; then :; fi
  rm -rf "${ICONSET:h}"
fi

# Identité du paquet : osacompile n'écrit AUCUN CFBundleIdentifier et
# nomme l'exécutable « droplet » — macOS (TCC) ne sait alors pas
# mémoriser les autorisations. Un identifiant stable + un nom d'affichage
# règlent ça (mêmes valeurs que dans le hub : io.stargazer.<id>).
PLIST="$NOM.app/Contents/Info.plist"
NOM_AFFICHE="$(/usr/bin/plutil -extract nom raw manifest.json 2>/dev/null \
  || print -r -- "SkadooshConverter")"
VERSION="$(cat VERSION 2>/dev/null | tr -d '[:space:]' || true)"
pb() { /usr/libexec/PlistBuddy -c "Set :$1 $2" "$PLIST" >/dev/null 2>&1 ||
      /usr/libexec/PlistBuddy -c "Add :$1 string $2" "$PLIST" >/dev/null 2>&1; }
pb CFBundleIdentifier "io.stargazer.$ID_APP"
pb CFBundleName "$NOM"
pb CFBundleDisplayName "$NOM_AFFICHE"
[[ -n "$VERSION" ]] && pb CFBundleShortVersionString "$VERSION"

# Icône : osacompile embarque un Assets.car dont le CFBundleIconName
# ÉCRASE notre .icns, et les applets « droplet » regardent droplet.icns,
# pas applet.icns. On purge le catalogue, on normalise sur applet.icns.
rm -f "$NOM.app/Contents/Resources/Assets.car" \
      "$NOM.app/Contents/Resources/droplet.icns"
/usr/libexec/PlistBuddy -c "Delete :CFBundleIconName" "$PLIST" >/dev/null 2>&1 || true
pb CFBundleIconFile "applet"

# signature ad hoc (aucun certificat requis) : l'identité reste stable
# tant qu'on ne recompile pas
codesign --force -s - "$NOM.app" >/dev/null 2>&1 || true

echo "Build OK -> $PWD/$NOM.app"
