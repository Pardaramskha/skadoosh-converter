# =====================================================================
#  install-pandoc.ps1
#  Télécharge un Pandoc portable (conversion de formats texte) et dépose
#  pandoc.exe dans le dossier de dépendances. Aucune modification du
#  PATH, aucune installation système : parfait pour une clé USB.
#
#  Source fiable : l'API GitHub jgm/pandoc, asset « *-windows-x86_64.zip »
#  (même patron que l'installation d'ImageMagick).
#
#  Lancement autonome :
#    powershell -ExecutionPolicy Bypass -File install-pandoc.ps1
#  (Le bouton « Installer les dépendances » l'appelle pour vous.)
# =====================================================================

param(
    [string]$DestDir
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Root      = Split-Path -Parent $ScriptDir
# Dossier des dependances : partage (<hub>\dependencies) quand l'app vit
# dans une installation Stargazer complete, sinon bin\ local (autonome).
if (-not $DestDir) {
    $HubRoot = Split-Path -Parent (Split-Path -Parent $Root)
    if ($HubRoot -and (Test-Path (Join-Path $HubRoot 'Stargazer.exe'))) {
        $DestDir = Join-Path $HubRoot 'dependencies'
    } else {
        $DestDir = Join-Path $Root 'bin'
    }
}

$work = Join-Path $env:TEMP ('pandoc_dl_' + $PID)
$zip  = Join-Path $work 'pandoc.zip'

try {
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Force -Path $DestDir | Out-Null }
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    Write-Host "Recherche de la dernière version de Pandoc..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/jgm/pandoc/releases/latest' -UseBasicParsing
    $asset = $release.assets | Where-Object { $_.name -like '*-windows-x86_64.zip' } | Select-Object -First 1
    if (-not $asset) { throw "archive Windows introuvable dans la release Pandoc." }

    Write-Host "Téléchargement de $($asset.name) (cela peut prendre une minute)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing

    Write-Host "Extraction..."
    Expand-Archive -Path $zip -DestinationPath $work -Force

    $found = Get-ChildItem -Path $work -Recurse -Filter 'pandoc.exe' | Select-Object -First 1
    if (-not $found) { throw "pandoc.exe introuvable dans l'archive téléchargée." }
    Copy-Item -Path $found.FullName -Destination (Join-Path $DestDir 'pandoc.exe') -Force

    Write-Host "Pandoc est prêt dans : $DestDir"
    exit 0
}
catch {
    Write-Host "Échec de l'installation de Pandoc : $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path $work) { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
}
