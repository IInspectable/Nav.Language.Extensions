@echo off
REM One-Shot: baut den self-contained Nav-LSP-Server, bettet ihn in die VS-Code-Extension ein und
REM erzeugt ein installierbares, plattform-spezifisches VSIX nach deploy\vscode.
REM Voraussetzung: Node/npm im PATH (fuer npm install + npx @vscode/vsce) sowie Visual Studio (MSBuild)
REM fuer den Server-Build (siehe Publish-Lsp.bat).
chcp 65001 >nul
setlocal

set "config=%1"
if "%config%" == "" set "config=Release"

set "root=%~dp0"
set "extdir=%root%vscode-nav-lsp"
set "serverexe=%root%deploy\lsp\nav.lsp.exe"
set "vsixdir=%root%deploy\vscode"
set "vsixname=nav-language-1.0.0-win32-x64.vsix"

REM 1) Server frisch self-contained publizieren (deploy\lsp\nav.lsp.exe).
call "%root%Publish-Lsp.bat" %config%
if errorlevel 1 (
    echo.
    echo Server-Publish fehlgeschlagen - VSIX wird nicht erzeugt.
    exit /b 1
)

if not exist "%serverexe%" (
    echo.
    echo Erwartete Server-Datei nicht gefunden: %serverexe%
    exit /b 1
)

REM 2) Laufzeit-Abhaengigkeiten der Extension sicherstellen (idempotent).
pushd "%extdir%"
call npm install
if errorlevel 1 (
    echo.
    echo npm install fehlgeschlagen.
    popd
    exit /b 1
)
popd

REM 3) Server in die Extension einbetten (server\nav.lsp.exe neben extension.js).
if exist "%extdir%\server" rmdir /s /q "%extdir%\server"
mkdir "%extdir%\server"
copy /y "%serverexe%" "%extdir%\server\nav.lsp.exe" >nul
if errorlevel 1 (
    echo.
    echo Kopieren des Servers in die Extension fehlgeschlagen.
    exit /b 1
)

REM 4) VSIX paketieren (plattform-spezifisch win32-x64, passend zum win-x64 self-contained Server).
REM    'echo y' beantwortet etwaige vsce-Bestaetigungsprompts nicht-interaktiv.
if not exist "%vsixdir%" mkdir "%vsixdir%"
pushd "%extdir%"
echo y | call npx @vscode/vsce package --target win32-x64 --out "%vsixdir%\%vsixname%"
if errorlevel 1 (
    echo.
    echo vsce package fehlgeschlagen.
    popd
    exit /b 1
)
popd

echo.
echo VS-Code-Paket erzeugt: %vsixdir%\%vsixname%
echo Installation: VS Code ^> Extensions ^> "Install from VSIX..." ^> obige Datei waehlen.
endlocal
