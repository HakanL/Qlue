PUSHD Qlue.Core
C:\Temp\nuget.exe pack Qlue.Core.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Qlue.Client
C:\Temp\nuget.exe pack Qlue.Client.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD

PUSHD Qlue.Server
C:\Temp\nuget.exe pack Qlue.Server.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory ..\_NuGetOutput\
POPD
