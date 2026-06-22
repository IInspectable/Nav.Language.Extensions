@Echo off
call build.bat
REM call Run-Tests.bat
xcopy ".\deploy\Build Tools" C:\ws\XTplus\z_Nav3\build\Script\Nav /I /Y
echo.
echo Deploy directory: %~dp0deploy\Build Tools