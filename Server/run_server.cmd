@echo off
setlocal

REM Aggiunge Shared al PATH solo per questa esecuzione
set "PATH=%~dp0..\Shared;%PATH%"

go run .

pause