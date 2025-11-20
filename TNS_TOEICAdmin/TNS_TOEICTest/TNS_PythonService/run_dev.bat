@echo off
echo ========================================
echo TOEIC Analysis Service - DEV MODE
echo ========================================
echo.

:: Change to script directory
cd /d %~dp0

:: Check if Python is installed
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Python is not installed or not in PATH
    echo Please install Python 3.12+ from https://www.python.org/
    echo.
    pause
    exit /b 1
)

echo [1/4] Checking Python version...
python --version

:: Create virtual environment if not exists
if not exist venv (
    echo.
    echo [2/4] Creating virtual environment...
    python -m venv venv
    if %errorlevel% neq 0 (
        echo ERROR: Failed to create virtual environment
        pause
        exit /b 1
    )
    echo Virtual environment created successfully!
) else (
    echo.
    echo [2/4] Virtual environment already exists
)

:: Activate virtual environment
echo.
echo [3/4] Activating virtual environment...
call venv\Scripts\activate.bat
if %errorlevel% neq 0 (
    echo ERROR: Failed to activate virtual environment
    pause
    exit /b 1
)

:: Upgrade pip
echo.
echo Upgrading pip...
python -m pip install --upgrade pip --quiet

:: Install dependencies
echo.
echo [4/4] Installing dependencies...
pip install -r requirements-dev.txt
if %errorlevel% neq 0 (
    echo ERROR: Failed to install dependencies
    pause
    exit /b 1
)

:: Start FastAPI server
echo.
echo ========================================
echo Starting FastAPI Development Server
echo ========================================
echo.
echo [RUNNING] http://localhost:5002
echo [API DOCS] http://localhost:5002/docs
echo [HEALTH] http://localhost:5002/health
echo.
echo Press CTRL+C to stop the server
echo ========================================
echo.

python -m uvicorn app.main:app --reload --host 0.0.0.0 --port 5002 --log-level info

pause