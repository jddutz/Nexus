Remove-Item ../../nuget/* -Recurse -Force -ErrorAction SilentlyContinue

dotnet build -c Release &&
dotnet test -c Release --no-build &&
dotnet pack -c Release --no-build -o ../../nuget