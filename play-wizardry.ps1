# Start a Wizardry-viewer session: table on one monitor, game on the other.
#
# Handles the things that are easy to get wrong by hand:
#   - only one process can hold 127.0.0.1:8787, so a stale viewer (or a Unity Play-mode session)
#     has to be out of the way first
#   - the game must run from its own bin directory, because it loads Data/Party and Data/Characters
#     by relative path and silently finds nothing from anywhere else
#   - the player window comes up DPI-scaled to roughly square; it gets resized to 16:9 here
#
# Order genuinely does not matter to the protocol -- the game republishes everything each turn --
# but starting the viewer first means the town board is up before the first keypress.

param(
    # Both halves live in this repo, so the defaults are relative to it and a clone works as-is.
    [string]$ViewerExe = (Join-Path $PSScriptRoot "WizardryViewer\Build\WizardryViewer.exe"),
    # Pass -GameDir to run a single branch's build instead of this working tree's.
    [string]$GameDir   = (Join-Path $PSScriptRoot "Adnd.Game\bin\Debug\net10.0-windows"),
    [int]   $Width     = 1600,
    [int]   $Height    = 900,
    [switch]$SameMonitor,
    # Force the window back to $Width x $Height on the chosen monitor, discarding wherever it was
    # left. Off by default: Unity already remembers position and size, and overriding that every
    # launch means the window visibly springs back from where you put it.
    [switch]$Place
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32 {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
}
"@

function Test-Port8787 {
    (Get-NetTCPConnection -LocalPort 8787 -State Listen -ErrorAction SilentlyContinue) -ne $null
}

# --- 1. clear the port -------------------------------------------------------
$existing = Get-Process WizardryViewer -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "A viewer is already running (pid $($existing.Id)) -- restarting it so this session is clean."
    $existing | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

if (Test-Port8787) {
    Write-Warning "Port 8787 is held by something else -- most likely Unity is in Play mode."
    Write-Warning "Press Stop in the Unity editor, then run this again. The viewer cannot bind twice."
    exit 1
}

# Build/ is not in the repo (it is output, and 100 MB of it), so a fresh clone has to produce the
# viewer once: open WizardryViewer/ in Unity 6000.4 and File > Build, target Windows, into Build/.
if (-not (Test-Path $ViewerExe)) { throw "Viewer not found: $ViewerExe (build WizardryViewer/ in Unity first)" }
$gameExe = Join-Path $GameDir "Adnd.Game.exe"
if (-not (Test-Path $gameExe)) { throw "Game not found: $gameExe (build the solution first)" }

# --- 2. viewer ---------------------------------------------------------------
Write-Host "Starting the viewer..."
$viewer = Start-Process $ViewerExe -PassThru

$deadline = [DateTime]::UtcNow.AddSeconds(30)
while (-not (Test-Port8787) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 400 }

if (-not (Test-Port8787)) {
    Write-Warning "The viewer did not open port 8787 within 30s. Check its Player.log:"
    Write-Warning "  $env:USERPROFILE\AppData\LocalLow\DefaultCompany\WizardryViewer\Player.log"
} else {
    Write-Host "  listening on 127.0.0.1:8787"
}

# Place it: left-hand monitor by default, so the game gets the primary screen.
$screens = [System.Windows.Forms.Screen]::AllScreens
$target  = if ($SameMonitor -or $screens.Count -lt 2) {
    [System.Windows.Forms.Screen]::PrimaryScreen
} else {
    ($screens | Where-Object { -not $_.Primary } | Select-Object -First 1)
}

$viewer.WaitForInputIdle(5000) | Out-Null
$viewer.Refresh()

# Unity persists window position and size under HKCU per company/product, so once the window has
# been placed by hand there is geometry worth keeping. Only position it on a first run, when there
# is nothing to preserve, or when -Place explicitly asks for it.
$geometryKey = "HKCU:\Software\DefaultCompany\WizardryViewer"
$remembered = $false
if (Test-Path $geometryKey) {
    $saved = Get-ItemProperty $geometryKey
    $remembered = ($saved.PSObject.Properties.Name -match "^Screenmanager Window Position X").Count -gt 0
}

if ($viewer.MainWindowHandle -eq 0) {
    Write-Host "  (no window handle yet; leaving placement alone)"
} elseif ($remembered -and -not $Place) {
    $px = ($saved.PSObject.Properties | Where-Object { $_.Name -like "Screenmanager Window Position X*" }).Value
    $py = ($saved.PSObject.Properties | Where-Object { $_.Name -like "Screenmanager Window Position Y*" }).Value
    $pw = ($saved.PSObject.Properties | Where-Object { $_.Name -like "Screenmanager Resolution Window Width*" }).Value
    $ph = ($saved.PSObject.Properties | Where-Object { $_.Name -like "Screenmanager Resolution Window Height*" }).Value
    Write-Host "  keeping your window where you left it (${pw}x${ph} at ${px},${py}) -- pass -Place to override"
} else {
    $x = $target.WorkingArea.X + [int](($target.WorkingArea.Width  - $Width)  / 2)
    $y = $target.WorkingArea.Y + [int](($target.WorkingArea.Height - $Height) / 2)
    # 0x0040 = SWP_SHOWWINDOW. Gives a sane 16:9 first run; the player's own default comes up
    # DPI-scaled to nearly square.
    [void][Win32]::SetWindowPos($viewer.MainWindowHandle, [IntPtr]::Zero, $x, $y, $Width, $Height, 0x0040)
    Write-Host "  window set to ${Width}x${Height} on $($target.DeviceName)"
}

# --- 3. game -----------------------------------------------------------------
Write-Host "Starting the game (from its own bin directory)..."
$game = Start-Process $gameExe -WorkingDirectory $GameDir -PassThru
Start-Sleep -Milliseconds 1200
$game.Refresh()
if ($game.MainWindowHandle -ne 0) {
    [void][Win32]::SetForegroundWindow($game.MainWindowHandle)
}

Write-Host ""
Write-Host "Ready. The game has the keyboard; the table follows along."
Write-Host "  M) Maze  -- go underground, the dungeon replaces the town board"
Write-Host "  Tab     -- in the viewer: follow-the-party vs whole-level framing"
Write-Host ""
Write-Host "viewer pid $($viewer.Id), game pid $($game.Id)"
