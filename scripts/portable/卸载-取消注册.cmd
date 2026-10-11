@echo off
chcp 65001 >nul
rem 取消注册一维输入法（不删除本文件夹里的 userdata 词库）
cd /d "%~dp0"
net session >nul 2>&1
if not %errorlevel%==0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
taskkill /im YiweiHelper.exe /f >nul 2>&1
YiweiServer.exe /quit >nul 2>&1
taskkill /im YiweiServer.exe /f >nul 2>&1
YiweiSetup.exe /u
reg delete "HKCU\Software\Yiwei\YiweiIME" /v Portable /f >nul 2>&1
echo 已取消注册。userdata 文件夹里的词库保留，可直接删除整个文件夹。
pause
