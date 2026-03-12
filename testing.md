# ModularPipelines Module Testing Guide

This guide summarizes findings on how to test modules within the `ModularPipelines` framework, ensuring isolation and maintainability.

## 1. Strategies for Test Isolation

Because `ModularPipelines` uses a module-based pipeline architecture with automatic dependency resolution (`DependsOn<T>`), testing modules in complete isolation requires specific strategies.

### A. Total Isolation: Direct `ExecuteAsync` Testing

For testing a module's core logic completely independently of the pipeline builder:

1. **Bypass the `PipelineHostBuilder`**: Do not register the module in a pipeline.
2. **Manually Instantiate**: Create an instance of your module class directly in the test.
3. **Mock `IModuleContext`**: Use a mocking library (like Moq) to create an instance of `IModuleContext`. Stub any necessary services or context-specific data.
4. **Invoke Directly**: Call the `protected internal` `ExecuteAsync` method directly on the module instance.
5. **Assert**: Validate the result or the interactions with your mocked context.

> **Note**: This approach effectively ignores the engine's lifecycle management and dependency graph, allowing for pure unit testing of the module logic.

### B. Integration Testing: Pipeline-Level Execution

For testing how a module interacts with the pipeline engine, its dependencies, or the DI container:

1. **Use `PipelineHostBuilder`**: Construct a pipeline using `TestPipelineHostBuilder`.
2. **Control Dependencies**:
    * **Dummy Modules**: Define minimal, private "dummy" module classes within your test class to satisfy `DependsOn` requirements.
    * **Optional Dependencies**: Mark dependencies as `[DependsOn<T>(Optional = true)]` if they aren't strictly required, so the `ModuleAutoRegistrar` ignores them.
3. **`TestBase.cs`**: Inherit from `TestBase` in your test classes. This provides:
    * Helper methods (`RunModule<T>`, `RunModules<...>`) for reduced boilerplate.
    * Automatic resource cleanup (`[After(Test)]` hook to dispose pipelines).

## 2. Dependency Management

The engine uses `ModuleAutoRegistrar` to automatically discover and register missing `[DependsOn(Optional = false)]` dependencies during pipeline construction.

* **To manage this in tests**: Provide the minimal required dependencies manually in the builder or use optional dependencies to prevent the registrar from over-provisioning the pipeline.

## 3. Infrastructure (`TestBase.cs`)

`TestBase` is the recommended base class for integration-style module tests. It centralizes:

* **Pipeline Lifecycle**: Ensures all pipelines created during tests are disposed of automatically.
* **Boilerplate Reduction**: Simplifies module registration, execution, and result extraction.
* **Service Resolution**: Provides easy access to resolve services from the container without full execution if needed.
