@echo off
setlocal

set config=%1
if "%config%" == "" set config=Debug

set "vswhere=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%vswhere%" (
    echo vswhere.exe nicht gefunden - ist Visual Studio 2022 installiert?
    exit /b 1
)

for /f "usebackq tokens=*" %%i in (`"%vswhere%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "msbuild=%%i"

if not defined msbuild (
    echo MSBuild.exe konnte nicht gefunden werden.
    exit /b 1
)

"%msbuild%" Nav.Language.Extensions.sln -t:restore -m
"%msbuild%" Nav.Language.Extensions.sln -p:Configuration="%config%" -v:n -m

endlocal
