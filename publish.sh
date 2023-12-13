#!/bin/bash

version="0.2.2"
fullName="dig"
names=("dig" "server")
src="src"
outputRoot="./publish"
framework="net8.0"
runTimes=("linux-arm64") # "osx-x64"

rm -rf $outputRoot
shopt -s globstar
rm -rf ./$src/**/bin/Release

publish_project() {
    name=$1
    runtime=$2
    dotnet publish ./$src/$name/$name.csproj -c Release -r $runtime --framework $framework --self-contained true /p:Version=$version /p:PublishReadyToRun=true /p:PublishSingleFile=true /p:PublishTrimmed=false /p:IncludeNativeLibrariesForSelfExtract=True /p:StripSymbols=true /p:PublishDir="bin\Release\$framework\$runtime" --output $outputRoot/standalone/$runtime
}

for runtime in "${runTimes[@]}"; do
    for name in "${names[@]}"; do
        publish_project $name $runtime
    done

    zip -r $outputRoot/$fullName-$version-$runtime.zip $outputRoot/standalone/$runtime/*
done
