# Testing Modules Independently in ModularPipelines

## Overview

ModularPipelines enables you to create modular build systems where each module can be tested independently. However, testing modules with dependencies requires careful consideration of the testing approach.

## The Problem with Mocking IModuleContext

When modules have dependencies, you might initially think to mock `IModuleContext`. However, this is extremely complex because:

### Large Interface Surface

`IModuleContext` extends `IPipelineContext` and includes:

- `Logger`, `Services`, `Summary` (basic properties)
- `Shell`, `Files`, `Data`, `Environment`, `Installers`, `Network`, `Security` (domain contexts)
- `GetModule<T>()`, `GetModuleIfRegistered<T>()` (dependency access)
- `SubModule()` methods

### Extension Methods Complexity

Modules often use extension methods like:

- `context.Git()` → returns `IGit` (with `Commands`, `Information`, `Versioning`, `RootDirectory`)
- `context.DotNet()` → returns `IDotNet` (with 40+ command methods)
- `context.Docker()`, `context.Kubernetes()`, etc.

Each of these interfaces has complex sub-interfaces that would need extensive mocking.

### Brittle Test Setup

```csharp
// DON'T DO THIS - Too complex and brittle
var mockContext = new Mock<IModuleContext>();
mockContext.Setup(c => c.Logger).Returns(mockLogger.Object);
mockContext.Setup(c => c.Services).Returns(mockServices.Object);
// ... dozens more setup calls needed
var mockGit = new Mock<IGit>();
var mockGitCommands = new Mock<IGitCommands>();
// ... even more setup
```

## Recommended Solution: Test Modules Approach

Instead of mocking the complex context, create **test-specific modules** that return controlled results. This approach:

- ✅ Avoids complex mocking
- ✅ Tests real module logic in pipeline context
- ✅ Works with `context.Git()`, `context.DotNet()`, etc.
- ✅ Is maintainable and readable
- ✅ Tests realistic module interactions

## Implementation Guide

### 1. Create Test Modules with Fixed Results

```csharp
// Test data models
public record GitInfo(string Branch, string Commit);
public record BuildResult(string ArtifactPath, bool Success);

// Test module that provides controlled Git data
public class TestGitModule : Module<GitInfo>
{
    private readonly GitInfo _gitInfo;

    public TestGitModule(GitInfo gitInfo)
    {
        _gitInfo = gitInfo;
    }

    protected internal override async Task<GitInfo?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        await Task.Yield(); // Ensure proper async behavior
        return _gitInfo;
    }
}
```

### 2. Create Module Under Test

```csharp
// Module that depends on Git information
[ModularPipelines.Attributes.DependsOn(typeof(TestGitModule))]
public class BuildModule : Module<BuildResult>
{
    protected internal override async Task<BuildResult?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        // Get dependency result
        var gitInfo = await context.GetModule<TestGitModule>();

        // Use real context extensions if needed
        var rootDir = context.Git().RootDirectory; // This works!

        // Your build logic
        var artifactPath = $"/artifacts/{gitInfo.ValueOrDefault?.Branch}";
        var success = gitInfo.ValueOrDefault?.Branch != "broken-branch";

        return new BuildResult(artifactPath, success);
    }
}
```

### 3. Write Independent Tests

```csharp
[TestFixture]
public class BuildModuleTests : TestBase
{
    [Test]
    public async Task BuildModule_Creates_Correct_Artifact_Path()
    {
        // Arrange: Create test dependency with specific data
        var testGit = new TestGitModule(new GitInfo("feature-x", "abc123"));

        // Act: Run both modules together in real pipeline
        var (git, build) = await RunModules<TestGitModule, BuildModule>();

        // Assert: Verify the build module used the git data correctly
        var buildResult = await build;
        await Assert.That(buildResult.ValueOrDefault?.ArtifactPath)
            .IsEqualTo("/artifacts/feature-x");
        await Assert.That(buildResult.ValueOrDefault?.Success).IsTrue();
    }

    [Test]
    public async Task BuildModule_Fails_On_Broken_Branch()
    {
        // Arrange: Different test scenario
        var testGit = new TestGitModule(new GitInfo("broken-branch", "bad"));

        // Act
        var (git, build) = await RunModules<TestGitModule, BuildModule>();

        // Assert
        var buildResult = await build;
        await Assert.That(buildResult.ValueOrDefault?.Success).IsFalse();
    }
}
```

## Advanced Patterns

### Multiple Dependencies

```csharp
// Test module for test results
public class TestTestResultsModule : Module<TestResults>
{
    private readonly TestResults _results;

    public TestTestResultsModule(TestResults results)
    {
        _results = results;
    }

    protected internal override async Task<TestResults?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return _results;
    }
}

// Module with multiple dependencies
[ModularPipelines.Attributes.DependsOn(typeof(TestGitModule))]
[ModularPipelines.Attributes.DependsOn(typeof(TestTestResultsModule))]
public class DeployModule : Module<bool>
{
    protected internal override async Task<bool?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        var gitInfo = await context.GetModule<TestGitModule>();
        var testResults = await context.GetModule<TestTestResultsModule>();

        // Deploy logic: require main branch and all tests passing
        return gitInfo.ValueOrDefault?.Branch == "main" &&
               testResults.ValueOrDefault?.Failed == 0;
    }
}

[Test]
public async Task DeployModule_Requires_Main_Branch_And_Passing_Tests()
{
    // Arrange
    var testGit = new TestGitModule(new GitInfo("main", "deploy123"));
    var testResults = new TestTestResultsModule(new TestResults(10, 0));

    // Act
    var (git, tests, deploy) = await RunModules<
        TestGitModule, TestTestResultsModule, DeployModule>();

    // Assert
    var deployResult = await deploy;
    await Assert.That(deployResult.ValueOrDefault).IsTrue();
}
```

### Constructor Injection for Services

For modules that need to mock specific services (not the entire context):

```csharp
public class BuildModule : Module<BuildResult>
{
    private readonly IGitService _gitService;

    // Allow dependency injection for testing
    public BuildModule(IGitService? gitService = null)
    {
        _gitService = gitService ?? new GitService();
    }

    protected internal override async Task<BuildResult?> ExecuteAsync(
        IModuleContext context, CancellationToken cancellationToken)
    {
        var gitInfo = await _gitService.GetInfoAsync();
        return new BuildResult { Path = $"/artifacts/{gitInfo.Branch}" };
    }
}

[Test]
public async Task BuildModule_With_Mocked_Service()
{
    var mockGit = new Mock<IGitService>();
    mockGit.Setup(g => g.GetInfoAsync())
           .ReturnsAsync(new GitInfo { Branch = "feature" });

    var module = new BuildModule(mockGit.Object);
    var result = await RunModule(module);

    await Assert.That(result.ValueOrDefault?.Path)
        .IsEqualTo("/artifacts/feature");
}
```

## Testing Strategy Layers

### 1. Unit Tests (Isolated Logic)

- Use constructor injection with mocked services
- Test individual methods in isolation
- Focus on business logic without pipeline concerns

### 2. Integration Tests (Module Interactions)

- Use Test Modules approach
- Test modules working together in pipeline
- Verify dependency resolution and data flow

### 3. End-to-End Tests (Full Pipeline)

- Test complete pipeline execution
- Use real modules with realistic data
- Verify overall system behavior

## Best Practices

### Test Module Naming

- Prefix with `Test` (e.g., `TestGitModule`, `TestDatabaseModule`)
- Make purpose clear (e.g., `SuccessfulBuildTestModule`, `FailingTestModule`)

### Test Data Management

- Use records or simple DTOs for test data
- Create helper methods for common test scenarios
- Keep test data realistic but deterministic

### Test Organization

```csharp
[TestFixture]
public class BuildModuleTests : TestBase
{
    [TestFixture]
    public class SuccessfulBuilds
    {
        [Test]
        public async Task Builds_On_Main_Branch() { /* ... */ }

        [Test]
        public async Task Builds_On_Feature_Branch() { /* ... */ }
    }

    [TestFixture]
    public class FailedBuilds
    {
        [Test]
        public async Task Fails_On_Broken_Branch() { /* ... */ }

        [Test]
        public async Task Fails_On_Missing_Dependencies() { /* ... */ }
    }
}
```

### Dependency Declaration

- Use `[DependsOn(typeof(TestModule))]` attributes
- Keep dependency declarations consistent between test and real modules
- Consider optional dependencies with `[DependsOn(typeof(TestModule), Optional = true)]`

## When to Use Context Mocking

Context mocking should be a **last resort** and only used for:

- Testing error handling when services are unavailable
- Mocking external API calls within module logic
- Testing specific service interaction edge cases

Even then, prefer service-level mocking over full context mocking.

## Summary

The Test Modules approach provides:

- **Simple Setup**: Create test modules with fixed results
- **Real Execution**: Run in actual pipeline context
- **Full Compatibility**: Works with all context extensions
- **Maintainable**: No brittle mock setup
- **Realistic**: Tests actual module interactions

This approach aligns with ModularPipelines' design philosophy of modular, testable components while avoiding the complexity of mocking large interface hierarchies.

## Testing with Mocked File System

ModularPipelines supports mocking file system operations for unit testing. All file I/O goes through `IFileSystemProvider`, which can be replaced with a mock implementation.

### Why Mock the File System?

- **Speed**: Tests run faster without actual disk I/O
- **Isolation**: Tests don't depend on file system state
- **Predictability**: No flaky tests due to file permissions or disk space
- **CI-friendly**: Works in any environment without file system setup

### Example: Mocking File Reads

```csharp
using Moq;
using ModularPipelines;
using ModularPipelines.FileSystem;
using ModularPipelines.Extensions;

[Test]
public async Task MyModule_ReadsConfigFile()
{
    // Create a mock provider
    var mockProvider = new Mock<IFileSystemProvider>();
    mockProvider.Setup(p => p.ReadAllTextAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("{\"setting\": \"value\"}");

    // Run pipeline with mock
    var builder = Pipeline.CreateBuilder(args);

    builder.Services.AddSingleton<IFileSystemProvider>(mockProvider.Object);
    builder.Services.AddModule<MyModule>();

    var result = await builder.Build().RunAsync();

    // Assert results
    Assert.That(result.Status, Is.EqualTo(PipelineStatus.Success));
}
```

### Example: Verifying File Writes

```csharp
[Test]
public async Task MyModule_WritesOutputFile()
{
    var mockProvider = new Mock<IFileSystemProvider>();

    var builder = Pipeline.CreateBuilder(args);

    builder.Services.AddSingleton<IFileSystemProvider>(mockProvider.Object);
    builder.Services.AddModule<OutputModule>();

    await builder.Build().RunAsync();

    // Verify the write occurred with expected content
    mockProvider.Verify(p => p.WriteAllTextAsync(
        It.Is<string>(path => path.Contains("output")),
        It.Is<string>(content => content.Contains("result")),
        It.IsAny<CancellationToken>()));
}
```

### Important Notes

- **Always use `context.Files`**: Files created via `context.Files.GetFile()` will use the injected provider. Files created directly via `new File("path")` use the real file system.

- **Provider Registration**: The mock provider must be registered before the pipeline runs. Using `services.AddSingleton<IFileSystemProvider>()` overrides the default `SystemFileSystemProvider`.

- **Mock ALL methods your code uses**: The mock provider only intercepts methods you explicitly set up. If your module calls `ReadAllTextAsync`, `FileExists`, and `Combine`, you must mock all three. Unmocked methods may throw or return default values depending on your mocking framework.

- **Implicit operators bypass mocking**: Implicit conversions like `File file = "/path/to/file"` create instances using the default `SystemFileSystemProvider`, not your mock. For full testability, always use `context.Files.GetFile()`.

- **Static methods are not mockable**: Methods like `File.GetNewTemporaryFilePath()` and `Folder.CreateTemporaryFolder()` use the real file system. Design your modules to receive paths via constructor or use `context.Files.CreateTemporaryFolder()` instead.

- **Mocking Path Operations**: If your code uses path operations, mock them too:

  ```csharp
  mockProvider.Setup(p => p.Combine(It.IsAny<string[]>()))
      .Returns((string[] paths) => Path.Combine(paths));
  ```

### What Gets Mocked

The `IFileSystemProvider` interface covers:

- File reads: `ReadAllTextAsync`, `ReadLinesAsync`, `ReadAllBytesAsync`
- File writes: `WriteAllTextAsync`, `WriteAllBytesAsync`, `WriteAllLinesAsync`, `AppendAllTextAsync`
- File management: `DeleteFile`, `CopyFile`, `MoveFile`, `FileExists`
- Directory operations: `CreateDirectory`, `DeleteDirectory`, `MoveDirectory`, `DirectoryExists`
- Enumeration: `EnumerateFiles`, `EnumerateDirectories`
- Path utilities: `GetTempPath`, `GetRandomFileName`, `Combine`, `GetRelativePath`

## IFileSystemProvider

```cs
namespace ModularPipelines.FileSystem;

/// <summary>
/// Provides low-level file system operations.
/// Inject a mock implementation for testing file system interactions.
/// </summary>
public interface IFileSystemProvider
{
    // File read operations
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    // File write operations
    Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    Task WriteAllBytesAsync(string path, byte[] contents, CancellationToken cancellationToken = default);
    Task WriteAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default);
    Task AppendAllTextAsync(string path, string contents, CancellationToken cancellationToken = default);
    Task AppendAllLinesAsync(string path, IEnumerable<string> contents, CancellationToken cancellationToken = default);

    // File stream operations
    Stream OpenRead(string path);
    Stream Create(string path);
    Stream Open(string path, FileMode mode, FileAccess access);

    // File management operations
    void DeleteFile(string path);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite);
    void MoveFile(string sourcePath, string destinationPath);
    bool FileExists(string path);

    // Directory operations
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive);
    void MoveDirectory(string sourcePath, string destinationPath);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

    // Path operations
    string GetTempPath();
    string GetRandomFileName();
    string Combine(params string[] paths);
    string GetRelativePath(string relativeTo, string path);
}
```

## FilesContext

```cs
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using ModularPipelines.Context.Domains.Files;
using ModularPipelines.FileSystem;
using File = ModularPipelines.FileSystem.File;

namespace ModularPipelines.Context.Domains.Implementations;

/// <summary>
/// Provides file system operations with rich File and Folder return types.
/// </summary>
internal class FilesContext : IFilesContext
{
    private readonly IFileSystemContext _fileSystemContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilesContext"/> class.
    /// </summary>
    /// <param name="fileSystemContext">The file system context for basic file operations.</param>
    /// <param name="zip">The zip context for compression operations.</param>
    /// <param name="checksum">The checksum context for file checksum operations.</param>
    public FilesContext(
        IFileSystemContext fileSystemContext,
        IZipContext zip,
        IChecksumContext checksum)
    {
        _fileSystemContext = fileSystemContext;
        Zip = zip;
        Checksum = checksum;
    }

    /// <inheritdoc />
    public File GetFile(string path) => _fileSystemContext.GetFile(path);

    /// <inheritdoc />
    public Folder GetFolder(string path) => _fileSystemContext.GetFolder(path);

    /// <inheritdoc />
    public Folder GetFolder(System.Environment.SpecialFolder specialFolder) => _fileSystemContext.GetFolder(specialFolder);

    /// <inheritdoc />
    public IEnumerable<File> Glob(string pattern)
    {
        // Use the current directory as the root for globbing
        var currentDirectory = Directory.GetCurrentDirectory();
        var directoryInfo = new DirectoryInfo(currentDirectory);

        return new Matcher(StringComparison.OrdinalIgnoreCase)
            .AddInclude(pattern)
            .Execute(new DirectoryInfoWrapper(directoryInfo))
            .Files
            .Select(x => new File(Path.Combine(currentDirectory, x.Path)))
            .Distinct();
    }

    /// <inheritdoc />
    public IEnumerable<Folder> GlobFolders(string pattern)
    {
        // Use the current directory as the root for globbing
        var currentDirectory = Directory.GetCurrentDirectory();
        var directoryInfo = new DirectoryInfo(currentDirectory);

        // For folder globbing, we need to handle patterns that match directories
        // The Matcher is designed for files, so we match files and then extract unique parent directories
        // Alternatively, we enumerate directories and filter by pattern
        return EnumerateFoldersMatchingPattern(directoryInfo, pattern)
            .Select(x => new Folder(x.FullName))
            .Distinct();
    }

    /// <inheritdoc />
    public Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
        => System.IO.File.ReadAllTextAsync(path, cancellationToken);

    /// <inheritdoc />
    public Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
        => System.IO.File.WriteAllTextAsync(path, content, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(System.IO.File.Exists(path) || Directory.Exists(path));

    /// <inheritdoc />
    public IZipContext Zip { get; }

    /// <inheritdoc />
    public IChecksumContext Checksum { get; }

    /// <summary>
    /// Enumerates directories matching a glob pattern.
    /// </summary>
    /// <param name="rootDirectory">The root directory to search from.</param>
    /// <param name="pattern">The glob pattern to match.</param>
    /// <returns>An enumerable of matching directories.</returns>
    private static IEnumerable<DirectoryInfo> EnumerateFoldersMatchingPattern(DirectoryInfo rootDirectory, string pattern)
    {
        // Use Matcher to find files matching the pattern with a wildcard appended
        // This will match any file in directories that match the pattern
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase)
            .AddInclude(pattern.TrimEnd('/') + "/**/*");

        var result = matcher.Execute(new DirectoryInfoWrapper(rootDirectory));

        // Extract unique parent directories from matched files
        var matchedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in result.Files)
        {
            // Get the directory part of the matched file relative to root
            var relativePath = file.Path;
            var directoryPath = Path.GetDirectoryName(relativePath);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                // Walk up the directory path and check if it matches the original pattern
                var fullPath = Path.Combine(rootDirectory.FullName, directoryPath);
                if (Directory.Exists(fullPath))
                {
                    matchedDirs.Add(fullPath);
                }
            }
        }

        // Also try direct directory enumeration for patterns that might not have files
        try
        {
            // Convert glob pattern to search pattern (simplified)
            var searchPattern = pattern.Replace("**", "*").TrimEnd('/');
            if (searchPattern.Contains('/'))
            {
                searchPattern = Path.GetFileName(searchPattern);
            }

            foreach (var dir in rootDirectory.EnumerateDirectories(searchPattern, SearchOption.AllDirectories))
            {
                matchedDirs.Add(dir.FullName);
            }
        }
        catch (ArgumentException)
        {
            // Invalid search pattern characters - ignore and use only matcher results
        }

        return matchedDirs.Select(x => new DirectoryInfo(x));
    }
}
```
