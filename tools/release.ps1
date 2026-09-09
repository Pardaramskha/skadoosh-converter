# release.ps1 — fabrique ce qui part en release GitHub, dans dist\ :
#
#   skadoosh-converter-windows-portable.zip   l'archive « portable » Windows
#        (Stargazer la télécharge et la déballe dans apps\ ; à la main :
#        déballer n'importe où et lancer SkadooshConverter.exe)
#   SkadooshConverter-Setup-Windows.exe      l'installeur autonome Windows
#        (auto-extracteur qui embarque la même archive, crée les
#        raccourcis et l'entrée « Applications installées » — voir
#        setup-stub.cs)
#   SkadooshConverter-macOS-portable.zip     l'édition macOS : les sources de
#        l'applet (mac/) ; sur le Mac, « zsh build.sh » compile l'app
#        avec l'osacompile livré avec macOS
#   SkadooshConverter-Setup-macOS.command    l'auto-installeur macOS :
#        double-clic → déballe dans ~/Applications/Skadoosh converter et compile
#        l'applet sur place
#
#   powershell -ExecutionPolicy Bypass -File tools\release.ps1
#
# Aucun SDK requis : tout se compile avec le csc.exe livré avec Windows.
# (L'édition mac ne peut pas être compilée ici — osacompile n'existe que
# sur macOS — d'où des archives de sources qui se compilent à l'arrivée.)
#
# Les noms ne portent PAS la version (elle est dans le tag de la release
# et dans VERSION) : ainsi les boutons du README pointent sur
# releases/latest/download/<nom> et téléchargent toujours la dernière.
# Le hub, lui, choisit l'archive .zip de la release dont le nom contient
# l'id de l'app et « windows » (voir Depot.cs) — l'archive mac contient
# aussi l'id, d'où le mot-clé.

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$version = ([IO.File]::ReadAllText('VERSION')).Trim()
$versionMac = ([IO.File]::ReadAllText('mac\VERSION')).Trim()
if ($version -ne $versionMac) {
    throw "VERSION ($version) et mac\VERSION ($versionMac) doivent être identiques (une seule version pour les deux éditions)."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force 'dist' | Out-Null
$dist = (Resolve-Path 'dist').Path

# Zip entrée par entrée, noms en « / » (CreateFromDirectory sous
# PowerShell 5 écrit des « \ », illisibles hors Windows).
function Zipper($dossier, $zip) {
    if (Test-Path $zip) { Remove-Item -Force $zip }
    $racine = (Resolve-Path $dossier).Path.TrimEnd('\') + '\'
    $archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        foreach ($fichier in Get-ChildItem $dossier -Recurse -File) {
            $nom = $fichier.FullName.Substring($racine.Length).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $fichier.FullName, $nom, 'Optimal') | Out-Null
        }
    } finally { $archive.Dispose() }
}

function Copier($source, $stage) {
    $cible = Join-Path $stage $source
    New-Item -ItemType Directory -Force (Split-Path -Parent $cible) | Out-Null
    Copy-Item $source $cible
}

# ======================================================= Windows ======

# 1. compiler l'application
cmd /c .\tools\build.bat
if (-not (Test-Path 'SkadooshConverter.exe')) { throw 'Compilation ratée : SkadooshConverter.exe manque.' }

# 2. rassembler ce qui part dans l'archive : l'exe, le fichier de portage,
#    la version, l'icône et les scripts qui installent les moteurs (pas
#    les outils de build, pas les sources)
$stage = Join-Path 'dist' 'stage-win'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
$fichiers = @('SkadooshConverter.exe', 'skadoosh-converter.stargazer.json', 'VERSION',
              'assets\icon.png', 'assets\icon.ico',
              'scripts\install-ffmpeg.ps1', 'scripts\install-magick.ps1', 'scripts\install-pandoc.ps1')
foreach ($f in $fichiers) { Copier $f $stage }

$zipWin = Join-Path $dist "skadoosh-converter-windows-portable.zip"
Zipper $stage $zipWin
Remove-Item -Recurse -Force $stage

# 3. l'installeur : le talon compilé avec l'archive embarquée en ressource
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$setup = Join-Path $dist "SkadooshConverter-Setup-Windows.exe"
& $csc /nologo /target:winexe "/out:$setup" /optimize+ `
    /win32icon:assets\icon.ico `
    "/resource:$zipWin,app.zip" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll `
    tools\setup-stub.cs
if ($LASTEXITCODE -ne 0) { throw "Compilation de l'installeur ratée." }

# ========================================================= macOS ======

# 4. les sources de l'applet, fins de ligne en LF quoi qu'il arrive (un
#    zsh ou un JXA en CRLF ne démarre pas)
$stageMac = Join-Path 'dist' 'stage-mac'
if (Test-Path $stageMac) { Remove-Item -Recurse -Force $stageMac }
New-Item -ItemType Directory -Force $stageMac | Out-Null
$fichiersMac = @('mac\build.sh', 'mac\manifest.json', 'mac\VERSION', 'mac\icon.png',
                 'mac\src\main.js', 'mac\lib\host.js', 'mac\scripts\deps.sh')
$fichiersMac += Get-ChildItem 'mac\ui' -Recurse -File | ForEach-Object { $_.FullName.Substring((Get-Location).Path.Length + 1) }
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($f in $fichiersMac) {
    $cible = Join-Path $stageMac $f.Substring(4)   # sans le « mac\ »
    New-Item -ItemType Directory -Force (Split-Path -Parent $cible) | Out-Null
    if ($f -match '\.(sh|js|html|css|json|txt)$' -or $f -like '*VERSION') {
        $texte = [IO.File]::ReadAllText($f) -replace "`r`n", "`n"
        [IO.File]::WriteAllText($cible, $texte, $utf8)
    } else {
        Copy-Item $f $cible
    }
}
$zipMac = Join-Path $dist "SkadooshConverter-macOS-portable.zip"
Zipper $stageMac $zipMac
Remove-Item -Recurse -Force $stageMac

# 5. l'auto-installeur .command : un script zsh suivi du zip en base64
#    (même recette que la valise mac du hub). Le fichier est écrit en LF,
#    sans BOM, base64 en lignes de 76 caractères.
$entete = @'
#!/bin/zsh
# Skadoosh converter — installation macOS. Double-cliquez : l'app se déballe dans
# ~/Applications/Skadoosh converter et se compile sur place avec l'osacompile livré
# avec macOS (aucun SDK, rien d'installé dans le système). Si macOS boude
# un fichier venu d'ailleurs : clic droit → Ouvrir, une fois.
set -e
CIBLE="$HOME/Applications/Skadoosh converter"
if [[ -d "$CIBLE" ]]; then
  echo "Une installation existe déjà : $CIBLE"
  echo "Ses fichiers seront mis à jour (vos réglages sont conservés)."
fi
echo "Déballage…"
TMPZIP="$(mktemp -t skadoosh-setup).zip"
# le zip est collé sous la ligne __PAYLOAD__ de ce fichier, en base64
sed '1,/^__PAYLOAD__$/d' "$0" | base64 -d > "$TMPZIP"
mkdir -p "$CIBLE"
ditto -x -k "$TMPZIP" "$CIBLE"
rm -f "$TMPZIP"
chmod +x "$CIBLE/build.sh" "$CIBLE/scripts/deps.sh"
echo "Compilation de l'applet…"
cd "$CIBLE"
zsh build.sh
echo ""
echo "Skadoosh converter est installé !"
echo "Dossier : $CIBLE"
echo "Les moteurs (FFmpeg, Pandoc) se téléchargeront au premier lancement"
echo "dans ~/Library/Application Support/Stargazer/dependencies, dossier"
echo "partagé avec les autres applications de la famille."
open -R "$CIBLE/SkadooshConverter.app"
exit 0
__PAYLOAD__

'@
$command = Join-Path $dist "SkadooshConverter-Setup-macOS.command"
$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($zipMac), 'InsertLineBreaks') -replace "`r`n", "`n"
[IO.File]::WriteAllText($command, ($entete -replace "`r`n", "`n") + $b64 + "`n", $utf8)

Write-Host ''
Write-Host "Archive Windows  : $zipWin"
Write-Host "Installeur       : $setup"
Write-Host "Sources mac      : $zipMac"
Write-Host "Installeur mac   : $command"
Write-Host "Publication      : gh release create v$version `"$zipWin`" `"$setup`" `"$zipMac`" `"$command`" --title `"Skadoosh converter $version`""
