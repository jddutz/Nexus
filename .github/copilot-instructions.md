All classes shall be fully documented. Use <inheritdoc/> as appropriate, but include xaml documentation on all public and private properties and methods. Private fields do not need to be documented.

After making changes, use `dotnet test --collect:"Code Coverage"` to run and verify all tests succeed.

Export test coverage to reports using `dotnet-coverage merge "tests/UnitTests/TestResults/**/*.coverage" --output "tests/UnitTests/TestCoverage/coverage.cobertura.xml" --output-format cobertura`

All services should target minimum 60% test coverage.