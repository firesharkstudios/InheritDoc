cd InheritDoc
..\packages\NuGet.CommandLine.4.4.1\tools\NuGet.exe pack InheritDoc.csproj -Prop Configuration=Release -OutputDirectory \NuGet.local

cd ..\InheritDocLib
..\packages\NuGet.CommandLine.4.4.1\tools\NuGet.exe pack InheritDocLib.csproj -IncludeReferencedProjects -Prop Configuration=Release -OutputDirectory \NuGet.local
