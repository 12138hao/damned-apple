@echo off
rem One-click local unit test runner. ASCII only + CRLF enforced.
cd /d "%~dp0"
echo ============================================
echo  Running unit tests (GameRecordsTests) ...
echo ============================================
dotnet test tests/tests.csproj
echo.
echo ============================================
echo  Done. If you see "Passed!" above, all tests are green.
echo  The final summary may appear up there. Press any key to close.
pause >nul