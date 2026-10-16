$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..\UMLForge\UMLForge.sln'
dotnet --info
dotnet restore $solution
dotnet build $solution --configuration Release --no-restore
dotnet test $solution --configuration Release --no-build --logger "console;verbosity=normal"
