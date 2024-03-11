# Distributed-Internet-Gateway (DIG) Comprehensive Guide

## Introduction

DIG (Distributed-Internet-Gateway) serves as a Web 2.0 gateway to content hosted on the Chia datalayer, aiming to integrate into a decentralized, peer-to-peer (P2P), and verifiable content delivery network (CDN). It provides web interface access to the Chia datalayer, facilitates integration into a mesh network for decentralized content hosting, and offers cross-platform compatibility.

## Prerequisites

1. **Chia Node**: Running a DIG node requires a full Chia Node, including its wallet and datalayer. Understanding Chia node operations is recommended.
2. **Dedicated Server**: For reliability and optimal performance, running a DIG node on a dedicated, always-on server is recommended.
3. **.NET 8 SDK**: Necessary for building DIG from source.

## Getting Started

### Download

Latest DIG releases are available on the GitHub releases page: [Download DIG from GitHub Releases](https://github.com/Datalayer-Storage/Distributed-Internet-Gateway/releases)

### Supported Binaries

- Linux (Ubuntu): `dig.node-linux-arm64-<tag>.deb`, `dig.node-linux-arm64-<tag>.zip`, `dig.node-linux-x64-<tag>.deb`, `dig.node-linux-x64-<tag>.zip`
- Windows: `dig.node-win-x64-<tag>.msi`, `dig.node-win-x64-<tag>.zip`
- OSX: Not Available Yet, Maybe one day

### Installation

#### Windows

- **MSI**: The `.msi` installer will set up DIG as a Windows service.

#### Linux

- **DEB**: The `.deb` package will install DIG and include a templated `systemctl dig.node@.service`.

### Systemctl Usage for Linux

To enable and start the `dig.node@<user>.service` for a specific user (replace `<user>` with the actual username):
Following these steps will keep the node running in the background.

```bash
# Enable the service
sudo systemctl enable dig.node@<user>.service

# Reload systemctl to recognize the new service
sudo systemctl daemon-reload

# Start the service
sudo systemctl start dig.node@<user>.service
```

# DIG CLI Commands Guide

This guide provides a comprehensive overview of the Distributed-Internet-Gateway (DIG) Command Line Interface (CLI). DIG CLI is a tool for managing the Chia Data Layer Distributed Internet Gateway, enabling you to control server coins, hosts, datalayer.place configurations, local gateway server operations, and store subscriptions/mirrors directly from the command line.

## General Syntax

The general syntax for using the DIG CLI commands is as follows:

```bash
dig.node [command] [subcommand] [options] [arguments]
```

## Command Overview

### 1. Coins

Manage server coins associated with your DIG node.

#### Commands

- **Add**: Adds a new server coin.
  ```bash
  dig.node coins add [options]
  ```
- **Delete**: Deletes an existing server coin.
  ```bash
  dig.node coins delete [options]
  ```
- **List**: Lists all server coins associated with the node.
  ```bash
  dig.node coins list [options]
  ```

### 2. Host

Configure and manage host settings for the DIG node.

#### Commands

- **Check**: Verifies the accessibility of a specified mirror host.
  ```bash
  dig.node host check [host] [options]
  ```
- **Check Chia**: Checks the accessibility to Chia network endpoints.
  ```bash
  dig.node host check-chia [options]
  ```
- **Show Config**: Displays the current configuration of the host.
  ```bash
  dig.node host show-config [options]
  ```

### 3. Place

Manage datalayer.place configurations, allowing for seamless integration with the Chia datalayer.

#### Commands

- **Login**: Logs into datalayer.place.
  ```bash
  dig.node place login [options]
  ```
- **Logout**: Logs out of datalayer.place.
  ```bash
  dig.node place logout [options]
  ```
- **Show**: Displays details about the current datalayer.place configuration.
  ```bash
  dig.node place show [options]
  ```
- **Update**: Updates the IP address for your datalayer.place proxy.
  ```bash
  dig.node place update [options]
  ```

### 4. Server

Control the local gateway server's operations, such as starting, stopping, and checking the status.

#### Commands

- **Check**: Checks the current status of the DIG server.
  ```bash
  dig.node server check [options]
  ```
- **Start**: Starts the DIG server in a new process.
  ```bash
  dig.node server start [options]
  ```
- **Stop**: Stops the DIG server.
  ```bash
  dig.node server stop [options]
  ```
- **Restart**: Restarts the DIG server.
  ```bash
  dig.node server restart [options]
  ```

### 5. Stores

Manage subscriptions to stores and mirrors, including adding, removing, and listing stores.

#### Commands

- **Add**: Subscribes to a store and creates a server coin.
  ```bash
  dig.node stores add [options]
  ```
- **Remove**: Unsubscribes from a store and deletes its associated coin.
  ```bash
  dig.node stores remove [options]
  ```
- **Unsubscribe All**: Unsubscribes from all stores and deletes their coins.
  ```bash
  dig.node stores unsubscribe-all [options]
  ```
- **Unmirror All**: Removes all mirrors from subscribed stores.
  ```bash
  dig.node stores unmirror-all [options]
  ```
- **List**: Lists all subscribed stores, their mirrors, and associated coins.
  ```bash
  dig.node stores list [options]
  ```
- **Sync**: Synchronizes the DIG node with the data layer.
  ```bash
  dig.node stores sync [options]
  ```
- **Check Fee**: Checks the fee for adding a new mirror or coin.
  ```bash
  dig.node stores check-fee [options]
  ```

## Conclusion

Hosting a DIG node and mastering the DIG CLI commands contributes to a more decentralized, resilient, and user-centric internet, providing a bridge between traditional web services and the decentralized Chia datalayer.

### Note

- Chia and its logo are trademarks of Chia Network, Inc., in the U.S. and globally.
- Ensure a stable internet connection and adequate resources to effectively support both a Chia node and DIG.