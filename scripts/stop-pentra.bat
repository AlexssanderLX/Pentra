@echo off
title Parar Pentra
cd /d "%~dp0.."

echo Parando o Pentra (os dados sao preservados no volume)...
docker compose down

echo.
echo Pentra parado.
timeout /t 3 /nobreak >nul
exit /b 0
