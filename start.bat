@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo 🚀 启动 MyPersonalTool ...
dotnet run --project MyPersonalTool\MyPersonalTool.csproj
pause
