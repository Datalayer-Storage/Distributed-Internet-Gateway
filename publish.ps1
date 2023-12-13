$version = "0.2.2"
$fullName = "dig"
$names = @("dig", "server")
$src = "src"
$outputRoot = "./publish"
$framework = "net8.0"
$runTimes = @("win-x64") #, "linux-x64", "osx-x64")

Remove-Item $outputRoot -Recurse -Force
Remove-Item ./$src/bin/Release -Recurse -Force

function Publish-Project {
    param(
        [string]$name,        
        [string]$runtime
    )
    dotnet publish ./$src/$name/$name.csproj -c Release -r $runtime --framework $framework --self-contained true /p:Version=$version /p:PublishReadyToRun=true /p:PublishSingleFile=True /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=True /p:StripSymbols=true /p:PublishDir="bin\Release\$framework\$runtime\" --output $outputRoot/standalone/$runtime
}

foreach ($runTime in $runTimes) {    
    foreach ($name in $names) {
        Publish-Project -name $name -runtime "win-x64"
    }

    Compress-Archive -CompressionLevel Optimal -Path $outputRoot/standalone/$runtime/* -DestinationPath $outputRoot/$fullName-$version-$runtime.zip
}
# if ([System.Environment]::OSVersion.Platform -eq "Win32NT") {
#     # build the msi - win-x64 only
#     dotnet build ./MsiInstaller/MsiInstaller.wixproj -c Release -r win-x64 --output $outputRoot
#     Move-Item -Path $outputRoot/en-us/*.msi -Destination $outputRoot/$name-$version-service-win-x64.msi
# }
