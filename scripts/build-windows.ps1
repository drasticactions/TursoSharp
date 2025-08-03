# Build script for Windows DLLs
param()

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$BindingsDir = Join-Path $ProjectRoot "bindings"
$RuntimeDir = Join-Path $ProjectRoot "runtime"
$WindowsDir = Join-Path $RuntimeDir "windows"

Write-Host "Building turso_csharp DLL for Windows platforms..." -ForegroundColor Green
Write-Host "Project root: $ProjectRoot"

# Ensure we're in the bindings directory
Set-Location $BindingsDir

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
cargo clean

# Define Windows targets
$Targets = @(
    "x86_64-pc-windows-gnu",
    "x86_64-pc-windows-msvc",
    "aarch64-pc-windows-msvc"
)

# Install all required targets
Write-Host "Installing Rust targets..." -ForegroundColor Yellow
foreach ($target in $Targets) {
    Write-Host "Installing target: $target"
    rustup target add $target
}

# Build for all targets
Write-Host "Building for all Windows targets..." -ForegroundColor Yellow
foreach ($target in $Targets) {
    Write-Host "Building for target: $target"
    cargo build --release --target $target
}

# Create Windows runtime directory
Write-Host "Creating Windows runtime directory..." -ForegroundColor Yellow
if (-not (Test-Path $WindowsDir)) {
    New-Item -ItemType Directory -Path $WindowsDir -Force | Out-Null
}

# Copy the primary DLL (prefer MSVC x64 if available, fallback to GNU)
Write-Host "Copying turso_csharp.dll to runtime directory..." -ForegroundColor Yellow

$MsvcDll = Join-Path $BindingsDir "target\x86_64-pc-windows-msvc\release\turso_csharp.dll"
$GnuDll = Join-Path $BindingsDir "target\x86_64-pc-windows-gnu\release\turso_csharp.dll"
$DestinationDll = Join-Path $WindowsDir "turso_csharp.dll"

if (Test-Path $MsvcDll) {
    Write-Host "Using MSVC x64 build"
    Copy-Item $MsvcDll $DestinationDll -Force
}
elseif (Test-Path $GnuDll) {
    Write-Host "Using GNU x64 build"
    Copy-Item $GnuDll $DestinationDll -Force
}
else {
    Write-Host "❌ No Windows DLL found" -ForegroundColor Red
    exit 1
}

# Verify the DLL
Write-Host "Verifying Windows DLL..." -ForegroundColor Yellow
if (Test-Path $DestinationDll) {
    Write-Host "✅ Windows DLL created successfully!" -ForegroundColor Green
    Write-Host "Location: $DestinationDll"
    
    # Show file info
    Write-Host ""
    Write-Host "DLL info:"
    $FileInfo = Get-Item $DestinationDll
    Write-Host "  Size: $([math]::Round($FileInfo.Length / 1MB, 2)) MB"
    Write-Host "  Created: $($FileInfo.CreationTime)"
    Write-Host "  Modified: $($FileInfo.LastWriteTime)"
}
else {
    Write-Host "❌ Failed to create Windows DLL" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🎉 Windows DLL build completed successfully!" -ForegroundColor Green
Write-Host "The DLL can now be used with:"
Write-Host "  - net9.0 on Windows"
Write-Host "  - Any .NET target framework on Windows"