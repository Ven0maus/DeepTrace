# Code Coverage

## Prerequisites

Install the dotnet-coverage tool once:
```
dotnet tool install -g dotnet-coverage
```

## Run Coverage

```bash
dotnet-coverage collect "dotnet test tests/DeepTrace.Tests/DeepTrace.Tests" ^
  -o tests/DeepTrace.Tests/coverage.cobertura.xml -f cobertura
```

## Generate HTML Report

```bash
reportgenerator ^
  -reports:tests/DeepTrace.Tests/coverage.cobertura.xml ^
  -targetdir:tests/DeepTrace.Tests/coverage-report ^
  -reporttypes:Html
```

Open `tests/DeepTrace.Tests/coverage-report/index.html` in a browser.

## Notes

- Use `dotnet-coverage` (not `--collect:"Code Coverage"` or coverlet.collector).
  The SDK collector produces binary `.coverage` files that require conversion.
  Coverlet.collector interferes with Mono.Cecil assembly reads used by tests.
- Coverage data is not committed. Clean up after reviewing:
  ```bash
  Remove-Item tests/DeepTrace.Tests/TestResults -Recurse -Force
  Remove-Item tests/DeepTrace.Tests/coverage-report -Recurse -Force
  Remove-Item tests/DeepTrace.Tests/coverage.cobertura.xml -Force
  ```
