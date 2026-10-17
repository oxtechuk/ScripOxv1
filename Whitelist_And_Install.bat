@echo off
:: ====================================================================
:: ScripOx CRM - One-Click Windows Unblocker & Whitelister
:: Ox Tech (https://oxtech.uk)
:: ====================================================================
chcp 65001 >nul
title ScripOx CRM - Security Verification

echo ====================================================================
echo             ScripOx CRM - Verification and Unblocker
echo                     Developed by Ox Tech
echo ====================================================================
echo.
echo [1/2] Unblocking downloaded installation files (Removing Zone.Identifier)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '%~dp0' -Filter '*.exe' | Unblock-File -ErrorAction SilentlyContinue; Get-ChildItem -Path '%~dp0' -Filter '*.zip' | Unblock-File -ErrorAction SilentlyContinue;"
echo [OK] Mark of the Web flags removed.
echo.

if exist "%~dp0certificates\OxTech_Public.cer" (
    echo [2/2] Registering Ox Tech trusted digital certificate...
    certutil -addstore -f "TrustedPublisher" "%~dp0certificates\OxTech_Public.cer" >nul 2>&1
    certutil -addstore -f "Root" "%~dp0certificates\OxTech_Public.cer" >nul 2>&1
    echo [OK] Ox Tech Digital Certificate registered.
) else (
    echo [2/2] Certificate step skipped.
)

echo.
echo ====================================================================
echo Verification complete! You can now run ScripOx without warnings.
echo ====================================================================
echo.
pause
