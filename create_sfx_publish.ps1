param(
  [string]$PublishFolder = "..\bin\Release\publish",
  [string]$OutputExe = "..\Output\LanChat-Installer.exe",
  [string]$Password = $null
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot

if (-not (Test-Path (Resolve-Path $PublishFolder))) {
    Write-Error "Publish folder not found: $PublishFolder"
    exit 1
}

# Ensure 7z is available
$seven = Get-Command 7z.exe -ErrorAction SilentlyContinue
if (-not $seven) { Write-Error "7z.exe not found in PATH; install 7-Zip."; exit 1 }

# Generate random password if not supplied
if (-not $Password) {
    $Password = [System.Convert]::ToBase64String((New-Object System.Security.Cryptography.RNGCryptoServiceProvider).GetBytes(12)) -replace '[^A-Za-z0-9]','A'
}

# Create temp folder with published files
$temp = Join-Path $PSScriptRoot "_sfx_tmp"
if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
New-Item -ItemType Directory -Path $temp | Out-Null
Copy-Item -Path (Join-Path (Resolve-Path $PublishFolder) '*') -Destination $temp -Recurse -Force

# Create encrypted 7z archive containing publish folder contents
$archive = Join-Path $PSScriptRoot "LanChat_publish.7z"
if (Test-Path $archive) { Remove-Item $archive -Force }

Write-Host "Creating encrypted archive (this may take a moment)..."
& 7z.exe a -t7z $archive (Join-Path $temp '*') -mx=9 -p$Password -mhe=on | Out-Null

# Find 7z.sfx module
$sevenPath = (Get-Command 7z.exe).Path
$possibleSfx = @("$([System.IO.Path]::GetDirectoryName($sevenPath))\\7z.sfx", "$([System.IO.Path]::GetDirectoryName($sevenPath))\\..\\7z.sfx", "$([System.IO.Path]::GetDirectoryName($sevenPath))\\..\\..\\7z.sfx")
$sfx = $possibleSfx | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $sfx) { Write-Error "7z.sfx not found; ensure 7-Zip is installed and 7z.sfx is available."; exit 1 }

# Create simple SFX config to run LanChat.exe after extraction
$sfxConfig = @"
;!@Install@!UTF-8!
Title="Lan Chat Installer"
BeginPrompt="Enter installer password to extract and run."
RunProgram="LanChat.exe"
;!@InstallEnd@!
"@

$sfxConfigFile = Join-Path $PSScriptRoot 'config.txt'
$sfxConfig | Out-File -FilePath $sfxConfigFile -Encoding ASCII

# Compose SFX: sfx module + config + archive -> output EXE
$outExe = Join-Path $PSScriptRoot $OutputExe
$outExeFull = Resolve-Path -Path $outExe | Select-Object -ExpandProperty Path
if (Test-Path $outExeFull) { Remove-Item $outExeFull -Force }

$fsOut = [System.IO.File]::OpenWrite($outExeFull)
$fsOut.SetLength(0)

# write sfx
$fsSfx = [System.IO.File]::OpenRead($sfx)
$fsSfx.CopyTo($fsOut)
$fsSfx.Close()

# write config
$fsConfig = [System.IO.File]::OpenRead($sfxConfigFile)
$fsConfig.CopyTo($fsOut)
$fsConfig.Close()

# write archive
$fsArc = [System.IO.File]::OpenRead((Resolve-Path $archive))
$fsArc.CopyTo($fsOut)
$fsArc.Close()

$fsOut.Close()

Write-Host "Created SFX installer: $outExeFull"
Write-Host "Installer password: $Password"

# Clean temp
Remove-Item $temp -Recurse -Force
Remove-Item $archive -Force
Remove-Item $sfxConfigFile -Force

Pop-Location

# Output JSON for automated consumption
@{
    Installer = $outExeFull
    Password = $Password
} | ConvertTo-Json
