@echo off
rem P0 打样：一条命令跑完 13 项探针，产出 p0-report.md
setlocal
cd /d "%~dp0"

dotnet build src\P0.Plugin\P0.Plugin.csproj -c Release -v minimal || exit /b 1
dotnet build src\P0.Host\P0.Host.csproj -c Release -v minimal || exit /b 1
dotnet src\P0.Host\bin\Release\net10.0-windows\P0.Host.dll %*
