@echo off
REM Self-contained Publish des Nav-LSP-Servers nach deploy\lsp.
REM Erzeugt deploy\lsp\nav.lsp.exe samt gebündelter .NET-Runtime (keine separate Runtime-Installation nötig).
REM Publish läuft bewusst über die Full-Framework-MSBuild.exe (wie Build.bat): die Engine nutzt in
REM Nav.Language\CustomBuild.targets die CodeTaskFactory, die "dotnet build"/"dotnet publish" nicht kennt.
chcp 65001 >nul
setlocal

set "config=%1"
if "%config%" == "" set "config=Release"

set "rid=win-x64"
set "publishdir=%~dp0deploy\lsp"

set "vswhere=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%vswhere%" (
    echo vswhere.exe nicht gefunden - ist Visual Studio installiert?
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%vswhere%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "msbuild=%%i"

if not defined msbuild (
    echo MSBuild.exe konnte nicht gefunden werden.
    exit /b 1
)

REM Zielverzeichnis vorher leeren - der self-contained Publish räumt Altbestand nicht selbst auf.
if exist "%publishdir%" rmdir /s /q "%publishdir%"

REM PublishSingleFile  -> alles in eine nav.lsp.exe (managed + native + Runtime), keine losen DLLs.
REM IncludeNativeLibrariesForSelfExtract -> auch die nativen Runtime-DLLs in die exe einbetten.
REM EnableCompressionInSingleFile        -> die exe komprimieren (deutlich kleiner; minimaler Start-Overhead).
REM SatelliteResourceLanguages=en        -> die lokalisierten Satellite-Ressourcen (cs/de/es/.../zh-Hant)
REM                                         weglassen; der Server braucht sie nicht.
REM DebugType=embedded                    -> keine separate .pdb-Datei (Symbole liegen im Assembly).
"%msbuild%" "%~dp0Nav.Language.Server\Nav.Language.Server.csproj" -restore -t:Publish ^
    -p:Configuration=%config% -p:RuntimeIdentifier=%rid% -p:SelfContained=true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true -p:SatelliteResourceLanguages=en ^
    -p:DebugType=embedded ^
    -p:PublishDir="%publishdir%/" -v:m -m
if errorlevel 1 (
    echo.
    echo Publish fehlgeschlagen.
    exit /b 1
)

echo.
echo Self-contained LSP-Server veröffentlicht nach: %publishdir%
echo Finale ausführbare Datei: %publishdir%\nav.lsp.exe
endlocal
