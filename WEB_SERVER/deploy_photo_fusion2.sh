#!/bin/bash
set -euo pipefail

APP_DIR="${PAPER_LEGENDS_WEB_DIR:-/var/www/PaperLegend-WEB}"
BUILD_DIR="${UNITY_SERVER_BUILD_DIR:-${APP_DIR}/docker/unity-server/build}"
DOCKERFILE="${APP_DIR}/docker/unity-server/Dockerfile"
IMAGE_NAME="${ROOM_DOCKER_IMAGE:-paperlegends/unity-dedicated:latest}"
PROJECT_LABEL="com.paperlegends.project=true"
SERVER_EXECUTABLE="${SERVER_EXECUTABLE:-PaperLegendServer.x86_64}"

echo ""
echo "Paper Legends Unity Linux dedicated server docker build"
echo "App dir: ${APP_DIR}"
echo "Build dir: ${BUILD_DIR}"
echo "Image: ${IMAGE_NAME}"
echo ""

if ! command -v docker >/dev/null 2>&1; then
  echo "ERROR: docker command not found." >&2
  exit 1
fi

if [ ! -d "${APP_DIR}" ]; then
  echo "ERROR: App directory not found: ${APP_DIR}" >&2
  exit 1
fi

if [ ! -f "${DOCKERFILE}" ]; then
  echo "ERROR: Dockerfile not found: ${DOCKERFILE}" >&2
  exit 1
fi

if [ ! -d "${BUILD_DIR}" ]; then
  echo "ERROR: Unity server build directory not found: ${BUILD_DIR}" >&2
  exit 1
fi

if [ ! -f "${BUILD_DIR}/${SERVER_EXECUTABLE}" ]; then
  echo "ERROR: Unity server executable not found: ${BUILD_DIR}/${SERVER_EXECUTABLE}" >&2
  echo "Set SERVER_EXECUTABLE if the Linux build executable has another name." >&2
  exit 1
fi

docker build \
  -f "${DOCKERFILE}" \
  -t "${IMAGE_NAME}" \
  --label "${PROJECT_LABEL}" \
  --build-arg SERVER_EXECUTABLE="${SERVER_EXECUTABLE}" \
  "${APP_DIR}"

echo ""
echo "Build completed: ${IMAGE_NAME}"
