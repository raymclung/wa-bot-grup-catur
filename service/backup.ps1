# ============================================================
#  Backup state penting WA Bot ke arsip ZIP bertimestamp.
#  Yang dibackup (yang TIDAK bisa dibuat ulang):
#    - gateway/auth          (sesi WhatsApp; hilang = scan QR ulang)
#    - gateway/config.json
#    - brain/config/         (config.json, rules.json)
#    - brain/data/*.json      (KECUALI puzzles.json yang regenerable)
#  Disimpan di  wa-bot\backups\  dan menyimpan KeepDays terakhir.
#
#  Jalan manual : powershell -ExecutionPolicy Bypass -File service\backup.ps1
#  Tidak butuh admin.
# ============================================================

$ErrorActionPreference = 'Stop'
$KeepDays = 14

$root    = Split-Path -Parent $PSScriptRoot         # folder wa-bot
$backups = Join-Path $root 'backups'
$null = New-Item -ItemType Directory -Force -Path $backups

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$stage = Join-Path $env:TEMP ("wabot-bkp-" + $stamp)
$null = New-Item -ItemType Directory -Force -Path $stage

function Copy-Into($src, $relDest) {
    if (Test-Path $src) {
        $dest = Join-Path $stage $relDest
        $parent = Split-Path -Parent $dest
        $null = New-Item -ItemType Directory -Force -Path $parent
        Copy-Item -Path $src -Destination $dest -Recurse -Force
    }
}

# Kumpulkan ke staging (struktur dipertahankan)
Copy-Into (Join-Path $root 'gateway\auth')          'gateway\auth'
Copy-Into (Join-Path $root 'gateway\config.json')   'gateway\config.json'
Copy-Into (Join-Path $root 'brain\config')          'brain\config'
Copy-Into (Join-Path $root 'brain\logs\audit.log')  'brain\logs\audit.log'   # jejak audit aksi admin

# brain\data\*.json KECUALI puzzles.json (5MB, bisa dibuat ulang via build_puzzle_pool.py)
$dataDest = Join-Path $stage 'brain\data'
$null = New-Item -ItemType Directory -Force -Path $dataDest
Get-ChildItem -Path (Join-Path $root 'brain\data') -Filter '*.json' -File |
    Where-Object { $_.Name -ne 'puzzles.json' } |
    ForEach-Object { Copy-Item $_.FullName -Destination $dataDest -Force }

# === SOURCE CODE (ditambah 2026-06-28 setelah insiden hilang-source) ===
# Agar source brain/gateway/service bisa DIPULIHKAN dari backup, bukan cuma decompile DLL.
$brainSrcDest = Join-Path $stage 'brain'
$null = New-Item -ItemType Directory -Force -Path $brainSrcDest
Get-ChildItem -Path (Join-Path $root 'brain') -Filter '*.cs' -File |
    ForEach-Object { Copy-Item $_.FullName -Destination $brainSrcDest -Force }
Copy-Into (Join-Path $root 'brain\WaBot.csproj')   'brain\WaBot.csproj'
Copy-Into (Join-Path $root 'gateway\src')          'gateway\src'
Copy-Into (Join-Path $root 'gateway\package.json') 'gateway\package.json'
Copy-Into (Join-Path $root 'service')              'service'
Copy-Into (Join-Path $root 'adapters')             'adapters'

# Buat ZIP
$zip = Join-Path $backups ("wa-bot-" + $stamp + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Remove-Item $stage -Recurse -Force

$sizeKb = [math]::Round((Get-Item $zip).Length / 1KB, 1)
Write-Host ("OK  : backup dibuat -> " + $zip + " (" + $sizeKb + " KB)")

# Prune backup lama (> KeepDays hari)
$cut = (Get-Date).AddDays(-$KeepDays)
$old = Get-ChildItem -Path $backups -Filter 'wa-bot-*.zip' -File | Where-Object { $_.LastWriteTime -lt $cut }
foreach ($f in $old) { Remove-Item $f.FullName -Force; Write-Host ("    hapus backup lama: " + $f.Name) }

Write-Host ("Selesai. Total arsip: " + (Get-ChildItem -Path $backups -Filter 'wa-bot-*.zip').Count)
