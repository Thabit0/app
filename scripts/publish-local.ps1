$ErrorActionPreference = 'Stop'
dotnet restore .\DawishContentStudio.sln
dotnet publish .\src\DawishContentStudio.Manager\DawishContentStudio.Manager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\Manager
dotnet publish .\src\DawishContentStudio.Agent\DawishContentStudio.Agent.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\Agent
Compress-Archive -Path .\publish\* -DestinationPath .\DawishContentStudio-Windows-NoApi.zip -Force
Write-Host 'Created DawishContentStudio-Windows-NoApi.zip'
