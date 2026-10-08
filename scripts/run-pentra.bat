@echo off
title Pentra
cd /d "%~dp0.."

echo ============================================
echo   Iniciando o Pentra...
echo ============================================
echo.

docker compose up --build -d
if errorlevel 1 (
    echo.
    echo [ERRO] Nao foi possivel iniciar.
    echo Verifique se o Docker Desktop esta aberto e tente novamente.
    echo.
    pause
    exit /b 1
)

echo.
echo Aguardando a aplicacao ficar pronta...
timeout /t 4 /nobreak >nul

start "" http://localhost:8080

echo.
echo Pentra rodando em http://localhost:8080
echo (Esta janela pode ser fechada. Para parar, use "Parar Pentra".)
timeout /t 4 /nobreak >nul
exit /b 0
