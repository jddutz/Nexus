---
name: run-test-coverage
description: 'Run and analyze Nexus .NET test coverage. Use when collecting Code Coverage, exporting Cobertura reports, checking class coverage, diagnosing coverage gaps, or ensuring each business-logic class meets the 70% minimum.'
argument-hint: 'Optional project, test scope, or class to analyze'
---

# Run Test Coverage

Collect Nexus test coverage, export a Cobertura report, and determine whether every applicable concrete production class meets the required 70% coverage threshold.

## Coverage Policy

The 70% class-coverage requirement applies to concrete production classes with executable business logic. Exclude test-source code, compiler-generated entries, interfaces, records, structs, enums, and abstract classes. Review an ordinary concrete class individually before excluding it as data-only; do not use an exclusion to bypass coverage for executable behavior.

## When to Use

- Run or investigate test coverage for Nexus.
- Export a Cobertura report for CI, a coverage viewer, or review.
- Check whether a class needs additional tests to meet the 70% policy.
- Analyze coverage gaps after changing production code.

## Procedure

1. Review the requested change and identify the affected concrete production classes. Apply the coverage-policy exclusions before assessing the 70% requirement.
2. Run all tests with coverage collection from the repository root:

   ```powershell
   dotnet test --collect:"Code Coverage"
   ```

3. Stop and report failing tests before interpreting coverage. Fix only failures that are in scope for the requested work, then rerun the command.
4. Merge the collected coverage files into the committed report location:

   ```powershell
   dotnet-coverage merge "tests/UnitTests/TestResults/**/*.coverage" --output "tests/UnitTests/TestCoverage/coverage.cobertura.xml" --output-format cobertura
   ```

5. Analyze the Cobertura report with the repository script. The optional final argument is the minimum line-coverage rate and defaults to `0.70`:

   ```powershell
   dotnet script scripts/Analyze-TestCoverage.csx -- "tests/UnitTests/TestCoverage/coverage.cobertura.xml" 0.70
   ```

   The script reports overall line and branch coverage, every applicable concrete class below the threshold, and excluded test, compiler-generated, interface, record, struct, enum, and abstract-class entries. Review any remaining ordinary concrete class manually before treating it as data-only.
6. For each applicable class below 70%, add or improve focused tests that exercise its uncovered business logic. Do not add artificial tests for interfaces or records with no business logic.
7. Repeat the collection and merge commands after test changes. Completion requires all tests to pass, the Cobertura report to be generated, and every applicable class to have at least 70% line coverage.

## Reporting Results

State the test outcome, the report path, each applicable class below 70% (if any), and the excluded-entry categories. Do not claim policy compliance if the report is missing or a required concrete production class was not evaluated.