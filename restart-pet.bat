@echo off
cd /d "%~dp0"
taskkill /F /IM Pokebar.DesktopPet.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start /B dotnet run --project Pokebar.DesktopPet/Pokebar.DesktopPet.csproj
