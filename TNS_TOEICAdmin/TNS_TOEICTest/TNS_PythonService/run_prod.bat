@echo off
echo ========================================
echo TOEIC Analysis Service - PRODUCTION
echo ========================================
echo.

:: Change to script directory
cd /d %~dp0

:: Check if virtual environment exists
if not exist venv (
    echo ERROR: Virtual environment not found!
    echo Please run run_dev.bat first to set up the environment
    echo.
    pause
    exit /b 1
)

:: Activate virtual environment
echo [1/3] Activating virtual environment...
call venv\Scripts\activate.bat
if %errorlevel% neq 0 (
    echo ERROR: Failed to activate virtual environment
    pause
    exit /b 1
)

:: Install production dependencies only
echo.
echo [2/3] Installing production dependencies...
pip install -r requirements.txt --quiet
if %errorlevel% neq 0 (
    echo ERROR: Failed to install dependencies
    pause
    exit /b 1
)

:: Start production server with multiple workers
echo.
echo [3/3] Starting production server...
echo.
echo ========================================
echo TOEIC Analysis Service - PRODUCTION
echo ========================================
echo.
echo [RUNNING] http://0.0.0.0:5002
echo [WORKERS] 4
echo [AUTO-RELOAD] Disabled
echo.
echo Press CTRL+C to stop the server
echo ========================================
echo.

python -m uvicorn app.main:app --host 0.0.0.0 --port 5002 --workers 4 --log-level warning

pause