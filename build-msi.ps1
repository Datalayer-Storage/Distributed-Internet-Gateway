$name = "Distributed-Internet-Gateway"
$src = "src"
$outputRoot = "./publish"

# build the msi - win-x64 only
if ([System.Environment]::OSVersion.Platform -eq "Win32NT") {
    # & ./publish.ps1 win-x64

    dotnet build ./$src/Installer/Windows/MsiInstaller.wixproj -c Release -r win-x64 --output $outputRoot
    Move-Item -Path $outputRoot/en-us/Distributed-Internet-Gateway.msi -Destination $outputRoot/$name-win-x64.msi
}
