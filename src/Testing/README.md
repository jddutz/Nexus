# Nexus.Testing

Testing provides support for constructing controlled runtime environments. It is a library used by tests, separate from the test projects under [tests](../../tests).

## Main types and behavior

- `TestRuntimeBuilder` implements `IRuntimeBuilder` using a supplied service collection.
- `AddServices` adds explicit service registrations; `AddConfiguration` supplies configuration.
- `Build` uses an empty configuration when none was supplied, registers GUI, EventHub, and runtime services, builds the provider, and resolves `INexusRuntime`.
- `PerformanceEventArgs` provides performance-related event data.

The builder does not register the full standard engine stack. Tests must provide the other dependencies required by the runtime, typically controlled substitutes. It still registers GUI and event infrastructure, so it is not an entirely empty service container. `UseVulkan` returns the builder without backend setup; `UseOpenGL` throws `NotImplementedException`.

## Dependencies and boundary

The .NET 10 project references [Core](../Core/README.md), [GUI](../GUI/README.md), and [Runtime](../Runtime/README.md), with Microsoft dependency injection. Keep reusable test setup here and scenario assertions in the actual test projects. Native rendering requirements depend on the services the test chooses to register.

## Development

From the repository root:

```sh
dotnet build src/Testing/Nexus.Testing.csproj
dotnet test tests/UnitTests/UnitTests.csproj
```

These commands were not executed for this documentation change. See [runtime tests](../../tests/UnitTests/Runtime), the [unit-test project](../../tests/UnitTests/UnitTests.csproj), and the [architecture baseline](../../README.md).
