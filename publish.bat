@echo off
chcp 65001 >nul
echo ===================================
echo  PTSnet 一键发布脚本
echo ===================================
echo.

set ROOT=%~dp0
set PUBLISH_DIR=%ROOT%publish_output

:: 清理上次发布
if exist "%PUBLISH_DIR%" (
    echo [1/4] 清理上次发布目录...
    rd /s /q "%PUBLISH_DIR%"
)
mkdir "%PUBLISH_DIR%"

:: 编译前端
echo [2/4] 编译前端...
cd /d "%ROOT%frontend"
call npm run build
if errorlevel 1 (
    echo [错误] 前端编译失败，请检查 npm 输出
    pause
    exit /b 1
)
echo       前端编译完成

:: 编译后端（Release）
echo [3/4] 编译后端...
cd /d "%ROOT%backend"
dotnet publish -c Release -o "%PUBLISH_DIR%" --nologo -v quiet
if errorlevel 1 (
    echo [错误] 后端编译失败，请检查 dotnet 输出
    pause
    exit /b 1
)
echo       后端编译完成

:: 把前端 dist 复制到后端 wwwroot
echo [4/4] 整合前端到发布目录...
xcopy /e /i /q "%ROOT%frontend\dist" "%PUBLISH_DIR%\wwwroot\"
if errorlevel 1 (
    echo [错误] 复制前端文件失败
    pause
    exit /b 1
)

echo.
echo ===================================
echo  发布成功！
echo  发布目录: %PUBLISH_DIR%
echo ===================================
echo.
echo 将 publish_output 文件夹整个复制到服务器，然后运行：
echo   PTSnet.exe
echo 或注册为 Windows 服务（推荐）：
echo   sc create PTSnet binPath="%PUBLISH_DIR%\PTSnet.exe" start=auto
echo   sc start PTSnet
echo.
pause
