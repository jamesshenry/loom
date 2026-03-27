# TODO

## `--clean` flag on CLI commands

Add a `--clean` flag so consumers can opt-in to a clean before any build target, rather than having to run `loom clean` separately.

**Files to change:**

- `src/Loom.Build/Commands.cs` — add `bool clean = false` parameter to command(s)
- `src/Loom.Build/Config/ExecutionOptions.cs` — add `bool Clean` property
- `src/Loom.Build/LoomConfig.cs` — update `GetPipelineCategories` to accept the flag:

  ```csharp
  public static string[] GetPipelineCategories(BuildTarget target, bool clean = false)
  {
      var categories = target switch { ... };
      return clean ? ["Clean", .. categories] : categories;
  }
  ```

**Usage:** `loom build --clean`, `loom publish --clean`, etc.

## `init` command should also generate github workflow files

- default behaviour: ignore if `.github/workflows` exists
- `--force` flag overwrites entire workflows folder
