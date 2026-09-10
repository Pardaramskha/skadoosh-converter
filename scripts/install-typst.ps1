# =====================================================================
#  install-typst.ps1
#  Télécharge un Typst portable (le moteur PDF des textes : Pandoc écrit
#  du Typst, Typst compose le PDF — polices embarquées, aucun LaTeX) et
#  dépose typst.exe dans le dossier de dépendances. Aucune modification
#  du PATH, aucune installation système : parfait pour une clé USB.
#
#  Source fiable : l'API GitHub typst/typst, asset
#  « typst-x86_64-pc-windows-msvc.zip » (même patron que Pandoc).
#
#  Lancement autonome :
#    powershell -ExecutionPolicy Bypass -File install-typst.ps1
#  (L'application l'appelle pour vous au premier besoin.)
# =====================================================================

param(
    [string]$DestDir
)

$ErrorActionPreference = 'Stop'

$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Root      = Split-Path -Parent $ScriptDir
# Dossier des dependances, PARTAGE par toutes les apps de la famille
# Stargazer (un moteur n'est jamais telecharge deux fois) : STARGAZER_DEPS
# s'il est donne, sinon <hub>\dependencies quand l'app vit dans Stargazer
# (Stargazer.exe deux niveaux au-dessus), sinon
# %LOCALAPPDATA%\Stargazer\dependencies (app autonome).
if (-not $DestDir) {
    if ($env:STARGAZER_DEPS) {
        $DestDir = $env:STARGAZER_DEPS
    } else {
        $HubRoot = Split-Path -Parent (Split-Path -Parent $Root)
        if ($HubRoot -and (Test-Path (Join-Path $HubRoot 'Stargazer.exe'))) {
            $DestDir = Join-Path $HubRoot 'dependencies'
        } else {
            $DestDir = Join-Path $env:LOCALAPPDATA 'Stargazer\dependencies'
        }
    }
}

$work = Join-Path $env:TEMP ('typst_dl_' + $PID)
$zip  = Join-Path $work 'typst.zip'

try {
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Force -Path $DestDir | Out-Null }
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    Write-Host "Recherche de la dernière version de Typst..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/typst/typst/releases/latest' -UseBasicParsing
    $asset = $release.assets | Where-Object { $_.name -eq 'typst-x86_64-pc-windows-msvc.zip' } | Select-Object -First 1
    if (-not $asset) { throw "archive Windows introuvable dans la release Typst." }

    Write-Host "Téléchargement de $($asset.name) (cela peut prendre une minute)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing

    Write-Host "Extraction..."
    Expand-Archive -Path $zip -DestinationPath $work -Force

    $found = Get-ChildItem -Path $work -Recurse -Filter 'typst.exe' | Select-Object -First 1
    if (-not $found) { throw "typst.exe introuvable dans l'archive téléchargée." }
    Copy-Item -Path $found.FullName -Destination (Join-Path $DestDir 'typst.exe') -Force

    Write-Host "Typst est prêt dans : $DestDir"
    exit 0
}
catch {
    Write-Host "Échec de l'installation de Typst : $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path $work) { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
}
