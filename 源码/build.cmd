@echo off
rem Build the exe into dist\ and run the self-test.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Test
pause
