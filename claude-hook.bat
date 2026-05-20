@echo off
chcp 65001 >nul
rem Claude Code 响应钩子：将响应传给桌面宠物
if "%~1"=="" exit /b
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0claude-hook.ps1" -Message "%~1"
