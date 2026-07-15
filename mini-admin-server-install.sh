#!/usr/bin/env bash
set -Eeuo pipefail

APP_NAME="MiniAdmin Installer"
REPOSITORY_URL="${MINIADMIN_REPO_URL:-https://gitee.com/baijincom/mini-admin.git}"
BRANCH="${MINIADMIN_BRANCH:-main}"
INSTALL_DIR="${MINIADMIN_INSTALL_DIR:-/opt/mini-admin}"
DEPLOY_ARGS=()

usage() {
  cat <<'EOF'
MiniAdmin 服务器安装/更新引导脚本

只需要把本文件上传到 Linux/1Panel 服务器，然后执行：
  bash mini-admin-server-install.sh

默认行为：
  1. 从 Gitee main 分支克隆或安全更新代码。
  2. 保留服务器已有 .env、MySQL、Redis 和上传文件数据卷。
  3. 调用仓库内 deploy.sh 构建、启动并执行健康检查。

安装器选项：
  --repo URL         代码仓库，默认 https://gitee.com/baijincom/mini-admin.git
  --branch NAME      部署分支，默认 main
  --dir PATH         安装目录，默认 /opt/mini-admin

可直接透传给 deploy.sh 的选项：
  --force-env        重新生成 .env，仅适用于没有旧 MySQL 数据的环境
  --skip-build       跳过镜像构建
  --no-cache         不使用 Docker 构建缓存
  --logs             部署成功后持续查看日志
  -h, --help         显示帮助

常用环境变量：
  MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/
  MINIADMIN_WEB_PORT=127.0.0.1:5666
  MINIADMIN_REPO_URL=https://gitee.com/baijincom/mini-admin.git
  MINIADMIN_BRANCH=main
  MINIADMIN_INSTALL_DIR=/opt/mini-admin

示例：
  MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/ bash mini-admin-server-install.sh
  bash mini-admin-server-install.sh --no-cache
EOF
}

log() {
  printf '\033[1;34m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

warn() {
  printf '\033[1;33m[%s]\033[0m %s\n' "$APP_NAME" "$*"
}

fail() {
  printf '\033[1;31m[%s]\033[0m %s\n' "$APP_NAME" "$*" >&2
  exit 1
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || fail "缺少命令：$1"
}

parse_args() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --repo)
        [[ $# -ge 2 ]] || fail "--repo 缺少 URL。"
        REPOSITORY_URL="$2"
        shift 2
        ;;
      --branch)
        [[ $# -ge 2 ]] || fail "--branch 缺少分支名。"
        BRANCH="$2"
        shift 2
        ;;
      --dir)
        [[ $# -ge 2 ]] || fail "--dir 缺少目录。"
        INSTALL_DIR="$2"
        shift 2
        ;;
      --force-env|--skip-build|--no-cache|--logs)
        DEPLOY_ARGS+=("$1")
        shift
        ;;
      -h|--help)
        usage
        exit 0
        ;;
      *)
        fail "未知参数：$1"
        ;;
    esac
  done
}

validate_environment() {
  need_command git
  need_command docker

  docker info >/dev/null 2>&1 || fail "Docker 服务未运行。请先在 1Panel 安装并启动 Docker。"
  docker compose version >/dev/null 2>&1 || fail "缺少 Docker Compose v2，请先完成 Docker 环境安装。"

  [[ -n "$REPOSITORY_URL" ]] || fail "代码仓库地址不能为空。"
  [[ -n "$BRANCH" ]] || fail "部署分支不能为空。"
  git check-ref-format --branch "$BRANCH" >/dev/null 2>&1 || fail "无效的 Git 分支名：${BRANCH}"
  [[ "$INSTALL_DIR" = /* ]] || fail "安装目录必须使用绝对路径。"
  [[ "$INSTALL_DIR" != "/" ]] || fail "安装目录不能是根目录。"
}

prepare_repository() {
  local parent_dir current_branch tracked_changes
  parent_dir="$(dirname "$INSTALL_DIR")"
  mkdir -p "$parent_dir" 2>/dev/null || fail "无法创建 ${parent_dir}，请使用 root 或有权限的账号执行。"

  if [[ ! -e "$INSTALL_DIR" ]]; then
    log "从 ${REPOSITORY_URL} 克隆 ${BRANCH} 到 ${INSTALL_DIR}。"
    git clone --branch "$BRANCH" --single-branch "$REPOSITORY_URL" "$INSTALL_DIR"
    return
  fi

  [[ -d "$INSTALL_DIR/.git" ]] || fail "${INSTALL_DIR} 已存在但不是 Git 仓库。请改用 --dir 指定空目录，或进入现有目录直接执行 bash deploy.sh。"

  tracked_changes="$(git -C "$INSTALL_DIR" status --porcelain --untracked-files=no)"
  [[ -z "$tracked_changes" ]] || fail "服务器仓库存在未提交修改，为避免覆盖已停止更新：${INSTALL_DIR}"

  log "更新服务器仓库到 ${REPOSITORY_URL} ${BRANCH}。"
  if git -C "$INSTALL_DIR" remote get-url origin >/dev/null 2>&1; then
    git -C "$INSTALL_DIR" remote set-url origin "$REPOSITORY_URL"
  else
    git -C "$INSTALL_DIR" remote add origin "$REPOSITORY_URL"
  fi
  git -C "$INSTALL_DIR" fetch --prune origin \
    "+refs/heads/${BRANCH}:refs/remotes/origin/${BRANCH}"

  current_branch="$(git -C "$INSTALL_DIR" symbolic-ref --quiet --short HEAD || true)"
  if [[ "$current_branch" != "$BRANCH" ]]; then
    if git -C "$INSTALL_DIR" show-ref --verify --quiet "refs/heads/${BRANCH}"; then
      git -C "$INSTALL_DIR" checkout "$BRANCH"
    else
      git -C "$INSTALL_DIR" checkout -b "$BRANCH" --track "origin/${BRANCH}"
    fi
  fi

  git -C "$INSTALL_DIR" merge --ff-only "origin/${BRANCH}"
}

run_deployment() {
  [[ -f "$INSTALL_DIR/deploy.sh" ]] || fail "仓库缺少 deploy.sh，请确认部署的是 MiniAdmin main 分支。"
  log "代码准备完成，开始 Docker Compose 部署。"
  cd "$INSTALL_DIR"
  exec bash deploy.sh "${DEPLOY_ARGS[@]}"
}

parse_args "$@"
validate_environment
prepare_repository
run_deployment
