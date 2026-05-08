@echo off
chcp 65001 > nul
echo ==========================================
echo   NIATEC Client - Установка
echo ==========================================
echo.

set "EXE=%~dp0ClientAccountApp.exe"
set "SHORTCUT=%USERPROFILE%\Desktop\NIATEC Client.lnk"

if not exist "%EXE%" (
    echo ОШИБКА: ClientAccountApp.exe не найден!
    echo Убедитесь что файл находится в той же папке что и этот скрипт.
    pause
    exit /b 1
)

echo Создаётся ярлык на рабочем столе...
powershell -Command "$ws = New-Object -COM WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%EXE%'; $s.WorkingDirectory = '%~dp0'; $s.Description = 'NIATEC Client - CRM'; $s.Save()"

if %errorlevel% == 0 (
    echo.
    echo ✓ Готово! Ярлык создан на рабочем столе.
    echo ✓ Можно запускать приложение.
) else (
    echo ОШИБКА при создании ярлыка.
)

echo.
pause
