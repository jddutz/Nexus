All classes shall be fully documented. Use <inheritdoc/> as appropriate, but include xaml documentation on all public and private properties and methods. Private fields do not need to be documented.

After making changes, use `dotnet test` to run and verify all tests succeed. We do not need to analyze code coverage for every change.

To test GraphicsSystem changes, we need to run HelloNexus, let it run for about 5s, then kill the process. We can check the debug console output for confirmation of behavior.

In Graphics, do not add logging to hot paths or inject `ILogger<T>` into graphics services. Keep diagnostic output confined to the existing Validation, PerformanceMetrics, and Diagnostics surfaces; retain `Debug.WriteLine` there so it is removed from distribution builds.