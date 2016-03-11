PUSHD Logging
C:\Temp\nuget.exe pack Logging.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Logging.NLog
C:\Temp\nuget.exe pack Logging.NLog.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Logging.Serilog
C:\Temp\nuget.exe pack Logging.Serilog.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Qlue.Core
C:\Temp\nuget.exe pack Qlue.Core.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Qlue.Client
C:\Temp\nuget.exe pack Qlue.Client.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD
