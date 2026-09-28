# Smoke test del vero PoopMan.exe su Windows: avvia il gioco, lo gioca inviando
# tasti reali alla finestra (come un giocatore) e salva screenshot del desktop.
# Fallisce se il processo si chiude da solo (crash) o non crea la finestra.
#
# Uso: pwsh tests/windows-smoke.ps1 -GameDir <cartella con PoopMan.exe> -OutDir <cartella output> [-PlaySeconds 90]
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [int]$PlaySeconds = 90
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
}
"@

$VK = @{ Enter = 0x0D; Esc = 0x1B; Space = 0x20; Left = 0x25; Up = 0x26; Right = 0x27; Down = 0x28; X = 0x58; C = 0x43 }
$script:shotN = 0

function Shot([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $file = Join-Path $OutDir ("{0:D2}_{1}.png" -f $script:shotN, $name)
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    $script:shotN++
    Write-Host "screenshot: $file"
}

function Focus-Game {
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {
        [Win]::ShowWindow($proc.MainWindowHandle, 9) | Out-Null   # SW_RESTORE
        [Win]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
    }
}

function Press([string]$key, [int]$holdMs = 70) {
    $vk = [byte]$VK[$key]
    [Win]::keybd_event($vk, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $holdMs
    [Win]::keybd_event($vk, 0, 2, [UIntPtr]::Zero)             # KEYEVENTF_KEYUP
    Start-Sleep -Milliseconds 60
}

function Assert-Alive([string]$phase) {
    if ($proc.HasExited) {
        Write-Host "::error::PoopMan.exe si e' chiuso durante '$phase' (exit code $($proc.ExitCode))"
        Get-WinEvent -LogName Application -MaxEvents 30 -ErrorAction SilentlyContinue |
            Where-Object { $_.ProviderName -in @('.NET Runtime', 'Application Error', 'Windows Error Reporting') } |
            Select-Object -First 5 | ForEach-Object { Write-Host "---- $($_.ProviderName) $($_.TimeCreated)"; Write-Host $_.Message }
        Shot "crash"
        exit 1
    }
}

$exe = Join-Path $GameDir 'PoopMan.exe'
if (-not (Test-Path $exe)) { Write-Host "::error::$exe non trovato"; exit 1 }
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
Write-Host "Schermo: $($screen.Width)x$($screen.Height)  -  avvio $exe"

$proc = Start-Process -FilePath $exe -WorkingDirectory $GameDir -PassThru

# Attende la finestra del gioco
$deadline = (Get-Date).AddSeconds(40)
while ((Get-Date) -lt $deadline) {
    Assert-Alive 'avvio'
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 500
}
if ($proc.MainWindowHandle -eq [IntPtr]::Zero) { Write-Host "::error::nessuna finestra dopo 40 s"; Shot "no_window"; exit 1 }
Write-Host "Finestra: '$($proc.MainWindowTitle)'"
Start-Sleep -Seconds 4
Focus-Game
Shot "title"

# Menu: Istruzioni avanti e indietro, poi GIOCA
Press Down; Press Down; Press Enter; Start-Sleep -Milliseconds 800; Shot "istruzioni"
Press Esc; Start-Sleep -Milliseconds 400
Press Up; Press Up; Press Enter
Start-Sleep -Seconds 3
Assert-Alive 'avvio partita'
Shot "partita"

# Gioco: movimenti casuali, bombe, big bomb, detonatore, pausa
$rng = New-Object System.Random 42
$dirs = @('Left', 'Up', 'Right', 'Down')
$start = Get-Date
$nextShot = 10
$pauseDone = $false
$restarts = 0
while (((Get-Date) - $start).TotalSeconds -lt $PlaySeconds) {
    Assert-Alive 'gioco'
    if ([Win]::GetForegroundWindow() -ne $proc.MainWindowHandle) { Focus-Game }
    Press $dirs[$rng.Next(4)] ($rng.Next(150, 700))
    $r = $rng.NextDouble()
    if ($r -lt 0.35) { Press Space }
    elseif ($r -lt 0.40) { Press X }
    elseif ($r -lt 0.45) { Press C }
    # ogni tanto ENTER: dopo un game over riparte (nome -> classifica -> nuova partita)
    if ($rng.NextDouble() -lt 0.04) { Press Enter; $restarts++ }

    $elapsed = ((Get-Date) - $start).TotalSeconds
    if (-not $pauseDone -and $elapsed -gt ($PlaySeconds / 2)) {
        Press Esc; Start-Sleep -Milliseconds 700; Shot "pausa"
        Press Esc; Start-Sleep -Milliseconds 400
        $pauseDone = $true
    }
    if ($elapsed -ge $nextShot) { Shot ("gioco_{0:D3}s" -f [int]$elapsed); $nextShot += 15 }
}

Assert-Alive 'fine'
$proc.Refresh()
Write-Host ("OK: PoopMan.exe ha girato {0} s senza chiudersi. Memoria: {1:N0} MB, CPU: {2:N1} s, ENTER premuti: {3}" -f `
    $PlaySeconds, ($proc.WorkingSet64 / 1MB), $proc.TotalProcessorTime.TotalSeconds, $restarts)
Shot "fine"
$proc.CloseMainWindow() | Out-Null
if (-not $proc.WaitForExit(10000)) { $proc.Kill() }
Write-Host "SMOKE PASS"
exit 0
