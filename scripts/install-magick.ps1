# =====================================================================
#  install-magick.ps1
#  Télécharge un ImageMagick portable officiel et dépose magick.exe dans
#  le dossier bin\ de l'application (lecture HEIC/HEIF/WebP/AVIF, écriture
#  WebP). Aucune modification du PATH, aucune installation système.
#
#  Source : les releases GitHub officielles (l'ancien index
#  imagemagick.org/archive/binaries/ a disparu, et les archives portables
#  n'existent plus qu'en .7z — extraites ici avec le tar.exe intégré à
#  Windows 10+, qui parle 7z via libarchive).
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

$Api  = 'https://api.github.com/repos/ImageMagick/ImageMagick/releases/latest'
$work = Join-Path $env:TEMP ('magick_dl_' + $PID)
$archive = Join-Path $work 'magick.7z'

try {
    # TLS 1.2 explicite : le PowerShell 5.1 d'un Windows fraîchement déballé
    # peut encore proposer du TLS 1.0 par défaut.
    [Net.ServicePointManager]::SecurityProtocol = `
        [Net.ServicePointManager]::SecurityProtocol -bor 3072

    if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Force -Path $DestDir | Out-Null }
    New-Item -ItemType Directory -Force -Path $work | Out-Null

    Write-Host "Recherche de la dernière version portable d'ImageMagick (GitHub)..."
    $rel = Invoke-RestMethod -Uri $Api -UseBasicParsing
    $asset = $rel.assets |
        Where-Object { $_.name -match '^ImageMagick-[\d\.\-]+-portable-Q16-x64\.7z$' } |
        Select-Object -First 1
    if (-not $asset) { throw "aucune archive portable-Q16-x64.7z dans la version $($rel.tag_name)" }
    Write-Host "Version retenue : $($asset.name)"

    Write-Host "Téléchargement (cela peut prendre une minute)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archive

    # Empreinte SHA-256 : l'API GitHub la publie (champ digest) — on la
    # vérifie dès qu'elle est présente, comme pour le Downloader.
    if ("$($asset.digest)" -match '^sha256:([0-9a-fA-F]{64})$') {
        $attendu = $Matches[1].ToLower()
        $reel = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLower()
        if ($reel -ne $attendu) { throw "empreinte SHA-256 inattendue (téléchargement corrompu ?)" }
        Write-Host "Empreinte SHA-256 vérifiée."
    }

    Write-Host "Extraction..."
    $tar = Join-Path $env:WINDIR 'System32\tar.exe'
    if (-not (Test-Path $tar)) { throw "tar.exe introuvable (Windows 10 1803 ou plus récent requis)" }
    $out = Join-Path $work 'out'
    New-Item -ItemType Directory -Force -Path $out | Out-Null
    & $tar -xf $archive -C $out 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "l'extraction de l'archive a échoué (tar, code $LASTEXITCODE)" }

    $found = Get-ChildItem -Path $out -Recurse -Filter 'magick.exe' | Select-Object -First 1
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
