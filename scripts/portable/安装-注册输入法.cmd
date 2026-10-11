@echo off
chcp 65001 >nul
rem 一维输入法 绿色便携版：把输入法注册到本机（需要管理员一次），用户词库放在本文件夹 userdata\
cd /d "%~dp0"
net session >nul 2>&1
if not %errorlevel%==0 (
  echo 需要管理员权限注册输入法，正在请求提升……
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
if not exist userdata mkdir userdata
reg add "HKCU\Software\Yiwei\YiweiIME" /v RimeUserDir /t REG_SZ /d "%~dp0userdata" /f >nul
reg add "HKCU\Software\Yiwei\YiweiIME" /v Portable /t REG_DWORD /d 1 /f >nul
taskkill /im YiweiServer.exe /f >nul 2>&1
YiweiSetup.exe /s
if errorlevel 1 ( echo 注册失败，请把本窗口截图反馈。 & pause & exit /b 1 )
start "" YiweiServer.exe
start "" YiweiDeployer.exe /deploy
start "" YiweiHelper.exe /background
echo.
echo 已注册。可以在任务栏输入法列表里选「一维输入法」。
echo 用户词库与设置保存在：%~dp0userdata
echo 换电脑时整个文件夹带走，在新电脑上再运行一次本脚本即可。
pause
