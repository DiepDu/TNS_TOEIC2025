@echo off
chcp 65001 >nul
echo ========================================
echo   TNS Content Processor Service
echo   Port: 5004
echo ========================================

cd /d %~dp0

if not exist "venv" (
    echo Creating virtual environment...
    python -m venv venv
)

call venv\Scripts\activate.bat

echo.
echo Upgrading pip...
python -m pip install --upgrade pip -q

echo.
echo Installing dependencies...
pip install -r requirements.txt

echo.
echo Starting server...
python -m uvicorn app.main:app --host 0.0.0.0 --port 5004 --reload