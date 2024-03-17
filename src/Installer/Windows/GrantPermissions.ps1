# Create a folder named .dig in the user's home directory
$digFolder = "$env:USERPROFILE\.dig"
if (-not (Test-Path $digFolder)) {
    New-Item -ItemType Directory -Path $digFolder | Out-Null
}

# create the user settings file with default cache directory location
$cachePath = Join-Path -Path $digFolder -ChildPath "store-cache"
$jsonContent = @{
    "dig" = @{
        "CacheProvider"      = "FileSystem"
        "FileCacheDirectory" = $cachePath
    }
}
$jsonString = $jsonContent | ConvertTo-Json -Depth 2
$filePath = Join-Path -Path $digFolder -ChildPath "appsettings.user.json"
$jsonString | Out-File -FilePath $filePath

# Grant NetworkService full control to .dig folder
icacls "$digFolder" /grant:r "NT AUTHORITY\NetworkService:(OI)(CI)(F)" /T

# Check if CHIA_ROOT environment variable exists and points to a folder
$chiaRoot = $env:CHIA_ROOT
$chiaMainnetFolder = Join-Path $env:USERPROFILE ".chia\mainnet"
if ($chiaRoot -and (Test-Path $chiaRoot)) {
    # Grant NetworkService modification access to CHIA_ROOT folder
    icacls $chiaRoot /grant "NT AUTHORITY\NetworkService:(M)" /T
}
# if not CHIA_ROOT set, check if ~/.chia/mainnet/ folder exists
elseif (Test-Path $chiaMainnetFolder) {
    # Grant NetworkService modification access to ~/.chia/mainnet/ folder
    icacls $chiaMainnetFolder /grant "NT AUTHORITY\NetworkService:(M)" /T
}
