@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
cd /d "%~dp0"

rem ============================================================
rem  CharonMascot - one click upload to GitHub
rem  Edit REPO_URL below if your repository address is different
rem ============================================================
set "REPO_URL=https://github.com/HBrOcean/CharonMascot.git"
set "BRANCH=main"
set "COMMIT_MSG=chore: update CharonMascot"

echo.
echo ==========================================
echo   CharonMascot  -^>  GitHub
echo ==========================================
echo   remote : %REPO_URL%
echo   branch : %BRANCH%
echo.

where git >nul 2>&1
if errorlevel 1 goto NO_GIT

if not exist ".git" goto INIT_REPO

echo [1/5] repository already initialized
goto CHECK_REMOTE

:INIT_REPO
echo [1/5] git init ...
git init -q
if errorlevel 1 goto FAIL_INIT
git symbolic-ref HEAD refs/heads/%BRANCH%
echo       done

:CHECK_REMOTE
git remote get-url origin >nul 2>&1
if errorlevel 1 (
    echo [2/5] add remote origin
    git remote add origin %REPO_URL%
    if errorlevel 1 goto FAIL_REMOTE
) else (
    echo [2/5] update remote origin
    git remote set-url origin %REPO_URL%
    if errorlevel 1 goto FAIL_REMOTE
)

git config user.name >nul 2>&1
if errorlevel 1 (
    set /p GITNAME=      your git user name  :
    if "!GITNAME!"=="" goto FAIL_USER
    git config user.name "!GITNAME!"
)
git config user.email >nul 2>&1
if errorlevel 1 (
    set /p GITMAIL=      your git email     :
    if "!GITMAIL!"=="" goto FAIL_USER
    git config user.email "!GITMAIL!"
)

echo [3/5] git add .
git add -A
if errorlevel 1 goto FAIL_ADD

echo [4/5] git commit
git diff --cached --quiet
if errorlevel 1 (
    git commit -q -m "%COMMIT_MSG%"
    if errorlevel 1 goto FAIL_COMMIT
) else (
    echo       nothing to commit, skip
)

echo [5/5] git push
git push -u origin %BRANCH%
if errorlevel 1 goto FAIL_PUSH

echo.
echo ==========================================
echo   SUCCESS  -^>  %REPO_URL%
echo ==========================================
echo.
echo   Tip: if this is a brand new empty repo, open
echo        Settings - Branches - Default branch, and switch it to '%BRANCH%'.
echo.
pause
exit /b 0

:NO_GIT
echo.
echo [ERROR] git not found in PATH.
echo         Install Git for Windows first: https://git-scm.com/download/win
echo.
pause
exit /b 1

:FAIL_INIT
echo.
echo [ERROR] git init failed.
goto END_FAIL
:FAIL_REMOTE
echo.
echo [ERROR] cannot set remote origin. Check the URL / network.
goto END_FAIL
:FAIL_USER
echo.
echo [ERROR] git user.name and user.email are required.
goto END_FAIL
:FAIL_ADD
echo.
echo [ERROR] git add failed.
goto END_FAIL
:FAIL_COMMIT
echo.
echo [ERROR] git commit failed.
goto END_FAIL
:FAIL_PUSH
echo.
echo [ERROR] git push failed.
echo         Common reasons:
echo           - repository does not exist on GitHub yet (create it first, empty, no README)
echo           - no permission / not logged in  (run: git config --global credential.helper manager)
echo           - network problem
goto END_FAIL

:END_FAIL
echo.
pause
exit /b 1
