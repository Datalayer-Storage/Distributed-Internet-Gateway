# DlMirrorSync

[![.NET](https://github.com/dkackman/DlMirrorSync/actions/workflows/dotnet.yml/badge.svg)](https://github.com/dkackman/DlMirrorSync/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/Datalayer-Storage/MirrorSync/actions/workflows/codeql.yml/badge.svg)](https://github.com/Datalayer-Storage/MirrorSync/actions/workflows/codeql.yml)

This is a utility service that will synchronize the list of chia data layer singletons from [datalayer.storage](https://api.datalayer.storage/mirrors/v1/list_all) to the local chia node. By running this tool, the local node will subscribe to and mirror all of the `datalayer.storage` singletons. This includes a transaction fee for each and devoting 0.0003 XCH per mirror, so be sure you are ready to do this.

Can either be run from code, from built binaries in [the latest release](https://github.com/dkackman/DlMirrorSync/releases/), or as a windows service.

- The `singlefile` versions require [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
- The `standalone` versions have .net embedded so don't need it installed separately.
- The MSI installs the windows service that will synchronize the singletons once per day. (this installs as autostart and will run immediately)

## Build

- Ensure you have the .NET 8 SDK installed
- Run `./publish.ps1` which will build single file and standalone binaries for windows, linux, and os-x
- Outputs will be placed in the `publish` folder

To build the installer you need wix installed: `dotnet tool install -g dotnet-wix` (Windows only).

To manually install as a windows service run `./install.ps1` from an elevated terminal.

## Run

### Command Line

If you are running the single file or standalone versions you can run the following command to sync the singletons:

```bash
./DlMirrorSync ["optional path to chia config"]
```

### Install As a Service

First build the binaries by running `publish.ps1` [[powershell for linux](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell-on-linux)]
which will build single file and standalone binaries for windows, linux, and os-x. Outputs will be placed in the `publish` folder. Second, set any options in the `appsettings.json` file in the relevant `./publish/standalone` folder. Then run the following commands to install the service:

#### Windows

To manually install as a windows service run `./install.ps1` from an elevated terminal.

#### Linux

To install as a `systemd` service run `sudo bash install.sh`

## Configuration

These settings can be configured in the `appsettings.json` file or via environment variables (prefixed with `DlMirrorSync:`).

- __MirrorServer__: true - If true, this node will mirror the singletons from datalayer.storage in addition to subscribing to them.
- __MirrorHostUri__: "" - The host uri to use for mirroring. If empty, will default to the host machine's public IP address.
- __WaitingForChangeDelayMinutes__: 2 - The number of minutes to wait if spendable balance is 0 but change is owed.
- __PollingIntervalMinutes__: 1440 - The number of minutes to wait between checking for new singletons.
- __MirrorServiceUri__: <https://api.datalayer.storage/mirrors/v1/list_all> - The uri to use for retrieving the list of singletons to mirror.
- __DefaultFee__: 500000, - The default fee to use for mirroring singletons if the dynamic fee cannot be retrieved.
- __AddMirrorAmount__: 300000001 - The number of mojos to reserve in the mirror coin for each singleton.
- __XchWalletId__: 1 - The XCH wallet id to use for paying the fee and reserve amount.
