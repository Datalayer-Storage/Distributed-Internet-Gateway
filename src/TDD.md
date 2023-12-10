# Distributed Internet Gateway Design Notes

## Design Considerations

- It is cross platform so should be able to run on any OS and feel native to that OS.
- Platform specific functionality (e.g. windows event log) should be conditionally enabled
- Services shall be individually configurable
- Built on the .NET host, it will follow those conventions (e.g. logging, configuration, DI, etc.)
  - <https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host>
  - <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host>

### Logging

<https://learn.microsoft.com/en-us/dotnet/core/extensions/logging>

### Configuration

<https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration>

### Dependency Injection

<https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection>

## Packages

Perhaps too cute but for now going to use DIG (Distributed Internet Gateway) as the root name for the project and executables.

### Executables

- `dig` - CLI tool for configuration and management
- `dig.server` - the web 2 to web 3 gateway
- `dig.sync` - service that syncs mirrors with `datalayer.storage` or other sync list
- `dig.ui` - internal UI for configuration and management (maybe windows only, maybe MAUI)

### Project Layout

    📁 src
    │    dig.sln - solution file
    │
    ├───📁 dig
    │       dig.csproj
    |
    ├───📁 server
    │       server.csproj
    |
    ├───📁 sync
    │       sync.csproj
    |
    ├───📁 ui
    │       ui.csproj
    |
    └───📁 installers
        |
        ├───📁 windows
        |       wix.csproj
        |
        └───📁 debian
                build.bash
