# =====================================================================
#  install-magick.ps1
#  Télécharge un ImageMagick portable officiel et dépose magick.exe dans
#  le dossier bin\ de l'application (lecture HEIC/HEIF/WebP/AVIF, écriture
#  WebP). Aucune modification du PATH, aucune installation système.
#
#  Lancement autonome :
#    powershell -ExecutionPolicy Bypass -File install-magick.ps1
#  (Le bouton « Installer les dépendances » l'appelle pour vous.)
# =====================================================================

param(
    [string]$DestDir
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Root      = Split-Path -Parent $ScriptDir
if (-not $DestDir) { $DestDir = Join-Path $Root 'bin' }

# Le nom du zip portable change à chaque version : on lit l'index officiel
# et on prend la dernière archive « portable-Q16-x64 ».
$IndexUrl = 'https://imagemagick.org/archive/binaries/'

$work = Join-Path $env:TEMP ('magick_dl_' + $PID)
$zip  = Join-Path $work 'magick.zip'

try {
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Force -Path $DestDir | Out-Null }
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    Write-Host "Recherche de la dernière version portable d'ImageMagick..."
    $index = (Invoke-WebRequest -Uri $IndexUrl -UseBasicParsing).Content
    $noms = [regex]::Matches($index, 'ImageMagick-[\d\.\-]+-portable-Q16-x64\.zip') |
        ForEach-Object { $_.Value } | Sort-Object -Unique
    if (-not $noms) { throw "aucune archive portable trouvée sur $IndexUrl" }
    $nom = $noms | Select-Object -Last 1
    Write-Host "Version retenue : $nom"

    Write-Host "Téléchargement (cela peut prendre une minute)..."
    Invoke-WebRequest -Uri ($IndexUrl + $nom) -OutFile $zip

    Write-Host "Extraction..."
    Expand-Archive -Path $zip -DestinationPath $work -Force

    $found = Get-ChildItem -Path $work -Recurse -Filter 'magick.exe' | Select-Object -First 1
    if (-not $found) { throw "magick.exe introuvable dans l'archive téléchargée." }
    Copy-Item -Path $found.FullName -Destination (Join-Path $DestDir 'magick.exe') -Force
    Write-Host "magick.exe installé."

    Write-Host ""
    Write-Host "ImageMagick est prêt dans : $DestDir"
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Échec de l'installation d'ImageMagick : $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path $work) { Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue }
}
