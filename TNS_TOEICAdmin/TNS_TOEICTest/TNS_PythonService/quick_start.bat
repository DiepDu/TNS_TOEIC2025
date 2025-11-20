@echo off
title TOEIC Analysis Service - Quick Start
color 0A

echo.
echo  ╔═══════════════════════════════════════════╗
echo  ║   TOEIC ANALYSIS SERVICE - QUICK START   ║
echo  ╚═══════════════════════════════════════════╝
echo.

cd /d %~dp0

:: Check Python
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python not found!
    echo Please install Python 3.12+ from https://www.python.org/
    pause
    exit /b 1
)

:: Setup or activate environment
if not exist venv (
    echo [SETUP] Creating environment... Please wait...
    python -m venv venv
    call venv\Scripts\activate.bat
    python -m pip install --upgrade pip --quiet
    pip install -r requirements-dev.txt
    echo.
    echo [SUCCESS] Setup complete!
) else (
    call venv\Scripts\activate.bat
)

:: Start server
echo.
echo  ╔═══════════════════════════════════════════╗
echo  ║            SERVER STARTING...             ║
echo  ╚═══════════════════════════════════════════╝
echo.
echo  [URL] http://localhost:5002
echo  [DOCS] http://localhost:5002/docs
echo  [HEALTH] http://localhost:5002/health
echo.
echo  Press CTRL+C to stop
echo.

python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 5002