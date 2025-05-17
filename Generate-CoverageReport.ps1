# Ensure test project path is correct
$testProject = "./CurrencyConverter.Tests/CurrencyConverter.Tests.csproj"

# Run tests with code coverage
dotnet test $testProject --collect:"XPlat Code Coverage"

# Find the generated coverage file
$coverageFile = Get-ChildItem -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1

if ($coverageFile -eq $null) {
    Write-Host "Coverage file not found."
    exit 1
}

# Generate HTML report
reportgenerator -reports:$coverageFile.FullName -targetdir:"./coverage-report" -reporttypes:Html

# Open the report in browser (Windows)
Start-Process "./coverage-report/index.html"
