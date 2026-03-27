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

```
dotnet loom init --force
```

Invoke pipeline by passing target as arg

```
dotnet loom test
```

configure loom.json to publish/pack artifacts

```
dotnet loom publish
```

artifacts are not clean unless explicitly set

```
dotnet loom clean # The Clean module
dotnet release --clean # Prepends the Clean module to pipeline
```
