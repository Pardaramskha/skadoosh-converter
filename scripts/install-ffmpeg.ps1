# =====================================================================
#  install-ffmpeg.ps1
#  Télécharge un FFmpeg portable et dépose ffmpeg.exe dans le dossier
#  bin\ de l'application. Aucune modification du PATH, aucune
#  installation système : parfait pour une clé USB.
#
#  Lancement autonome :
#    powershell -ExecutionPolicy Bypass -File install-ffmpeg.ps1
#  (Le bouton « Installer les dépendances » l'appelle pour vous.)
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

# Build « essentials » de gyan.dev - le paquet FFmpeg officiel compact.
$ZipUrl = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'

$work = Join-Path $env:TEMP ('ffmpeg_dl_' + $PID)
$zip  = Join-Path $work 'ffmpeg.zip'

try {
    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Force -Path $DestDir | Out-Null }
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    Write-Host "Téléchargement de FFmpeg (cela peut prendre une minute)..."
    Invoke-WebRequest -Uri $ZipUrl -OutFile $zip

    Write-Host "Extraction..."
    Expand-Archive -Path $zip -DestinationPath $work -Force

    $found = Get-ChildItem -Path $work -Recurse -Filter 'ffmpeg.exe' | Select-Object -First 1
    if (-not $found) { throw "ffmpeg.exe introuvable dans l'archive téléchargée." }
    Copy-Item -Path $found.FullName -Destination (Join-Path $DestDir 'ffmpeg.exe') -Force
    Write-Host "ffmpeg.exe installé."

    Write-Host ""
    Write-Host "FFmpeg est prêt dans : $DestDir"
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Échec de l'installation de FFmpeg : $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path $work) { Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue }
}
