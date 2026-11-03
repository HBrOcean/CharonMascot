@echo off
setlocal
cd /d "%~dp0"

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"
set "OUT=dist\%RID%"

echo ==========================================
echo   CharonMascot build  (RID=%RID%)
echo ==========================================

where dotnet >nul 2>&1
if errorlevel 1 goto NO_DOTNET

echo [1/2] restore
dotnet restore src\CharonMascot
if errorlevel 1 goto FAIL

echo [2/2] publish
dotnet publish src\CharonMascot -c Release -r %RID% --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUT%"
if errorlevel 1 goto FAIL

if not exist "%OUT%\CharonMascot.exe" goto FAIL

echo.
echo ==========================================
echo   OK  -^>  %OUT%\CharonMascot.exe
echo ==========================================
echo.
pause
exit /b 0

:NO_DOTNET
echo.
echo [ERROR] dotnet not found.
echo         Install .NET SDK 10.0 or newer: https://dotnet.microsoft.com/download
echo.
pause
exit /b 1

:FAIL
echo.
echo [ERROR] build failed. See messages above.
echo.
pause
exit /b 1
