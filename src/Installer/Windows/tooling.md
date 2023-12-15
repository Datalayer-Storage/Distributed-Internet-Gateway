# WiX

Built with the [WiX 4.x toolset](https://wixtoolset.org/)

Needs the [ui extension](https://wixtoolset.org/docs/tools/wixext/wixui/)

```powershell
dotnet tool install -g wix
wix extension add -g WixToolset.UI.wixext
```

The build command is

```powwershell
dotnet build ./MsiInstaller/MsiInstaller.wixproj -c Release -r win-x64 --output $outputRoot
```
