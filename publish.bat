@echo off
echo ===== Build CodeRunner =====
cd /d "%~dp0CodeRunner"
rmdir /s /q bin obj 2>nul
dotnet publish -c Release -r win-x64 --self-contained true -o ..\publish_temp\CodeRunner
if %errorlevel% neq 0 goto :error

echo ===== Build GameHMI =====
cd /d "%~dp0"
rmdir /s /q bin obj 2>nul
dotnet publish -c Release -r win-x64 --self-contained true -o publish_temp\GameHMI
if %errorlevel% neq 0 goto :error

echo ===== Copy CodeRunner to GameHMI =====
copy /y "publish_temp\CodeRunner\CodeRunner.exe" "publish_temp\GameHMI\CodeRunner.exe"

echo ===== Copy Course Data =====
xcopy /e /y "Data\courses" "publish_temp\GameHMI\Data\courses\"

echo ===== Package =====
if exist publish rmdir /s /q publish
ren publish_temp publish

echo.
echo ===== DONE =====
echo Output: %~dp0publish\GameHMI\
echo Run: GameHMI.exe
pause
goto :eof

:error
echo BUILD FAILED
pause
exit /b 1
