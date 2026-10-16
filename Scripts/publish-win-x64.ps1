$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\UMLForge\UMLForge.csproj'
dotnet publish $project --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=true
