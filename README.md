# Loom.Build

Opinionated build tool for dotnet projects using [Modular Pipelines](https://github.com/thomhurst/ModularPipelines)

## Installation

Install locally from repo root:

```bash
dotnet new tool-manifest
dotnet tool install loom
```

## Usage

```
dotnet loom --help
```

Use `init` to generate config and workflow files

```bash
dotnet loom init --force
```

## Subcommands

Loom provides subcommands for each build stage. You can run `dotnet loom [command] --help` for specific options.

### Test

Run your test suite:

```bash
dotnet loom test
```

### Build & Publish

Configure `loom.json` to define artifacts, then build or publish them:

```bash
dotnet loom build
dotnet loom publish
```

### Clean & Fresh Runs

Manual clean:

```bash
dotnet loom clean
```

Prepend the `Clean` module to any pipeline run using the `--fresh` flag:

```bash
dotnet loom release --fresh
```

### Global Options

Most commands support standard overrides:

```bash
dotnet loom build --rid win-x64
```
