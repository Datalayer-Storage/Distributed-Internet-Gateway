param(
    [string[]]$runTimes = @("win-x64", "linux-x64", "linux-arm64", "osx-x64")
)
# other runtimes that might work: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog

$version = "0.2.63"
$fullName = "dig"
$names = @("dig", "server")
$src = "src"
$outputRoot = "./publish"
$framework = "net8.0"

if (Test-Path -Path $outputRoot) {
    Remove-Item $outputRoot -Recurse -Force
}
function Publish-Project {
    param(
        [string]$name,        
        [string]$runtime
    )

    # dotnet clean ./$src/$name/$name.csproj -c Release -r $runtime -f $framework

    dotnet restore ./$src/$name/$name.csproj -r $runtime

    # fully standalone with embedded dotnet framework
    dotnet publish ./$src/$name/$name.csproj -c Release -r $runtime --no-restore --framework $framework --self-contained true /p:PublishReadyToRunComposite=true /p:Version=$version /p:PublishSingleFile=True /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=True /p:StripSymbols=true /p:PublishDir="bin\Release\$framework\$runtime\" --output $outputRoot/standalone/$runtime

    # single file without embedded dotnet framework
    # dotnet publish ./$src/$name/$name.csproj -c Release -r $runtime --no-restore --framework $framework --self-contained false /p:PublishReadyToRun=false /p:Version=$version /p:PublishSingleFile=True /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=True /p:StripSymbols=true /p:PublishDir="bin\Release\$framework\$runtime\" --output $outputRoot/singlefile/$runtime
}

foreach ($runTime in $runTimes) {    
    foreach ($name in $names) {
        Publish-Project -name $name -runtime $runTime
    }

    # zip up the standalone version
    Compress-Archive -CompressionLevel Optimal -Path $outputRoot/standalone/$runtime/* -DestinationPath $outputRoot/$fullName-$version-$runtime.zip
}
