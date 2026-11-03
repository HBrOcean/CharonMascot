#!/usr/bin/env bash
# CharonMascot - one click upload to GitHub (Linux / macOS)
# 修改下面的 REPO_URL 即可推送到自己的仓库
set -e
cd "$(dirname "$0")"

REPO_URL="${REPO_URL:-https://github.com/HBrOcean/CharonMascot.git}"
BRANCH="${BRANCH:-main}"
MSG="${MSG:-chore: update CharonMascot}"

echo "=========================================="
echo "  CharonMascot  ->  GitHub"
echo "  remote : $REPO_URL"
echo "  branch : $BRANCH"
echo "=========================================="

command -v git >/dev/null 2>&1 || { echo "[ERROR] git 未安装"; exit 1; }

if [ ! -d .git ]; then
  echo "[1/5] git init"
  git init -q
  git symbolic-ref HEAD "refs/heads/$BRANCH"
else
  echo "[1/5] 已存在 git 仓库"
fi

if git remote get-url origin >/dev/null 2>&1; then
  echo "[2/5] 更新 remote origin"
  git remote set-url origin "$REPO_URL"
else
  echo "[2/5] 添加 remote origin"
  git remote add origin "$REPO_URL"
fi

git config user.name >/dev/null 2>&1 || { echo "[ERROR] 请先设置 git user.name"; exit 1; }
git config user.email >/dev/null 2>&1 || { echo "[ERROR] 请先设置 git user.email"; exit 1; }

echo "[3/5] git add ."
git add -A

echo "[4/5] git commit"
if git diff --cached --quiet; then
  echo "      没有改动，跳过提交"
else
  git commit -q -m "$MSG"
fi

echo "[5/5] git push"
git push -u origin "$BRANCH"

echo
echo "SUCCESS -> $REPO_URL"
echo "提示：如果是新建的空仓库，去 Settings → Branches 把默认分支改成 $BRANCH"
