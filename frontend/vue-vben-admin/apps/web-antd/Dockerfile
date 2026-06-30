# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /app

ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0

ARG NPM_REGISTRY=https://registry.npmmirror.com
ARG PNPM_FETCH_TIMEOUT=600000
ARG PNPM_FETCH_RETRIES=5
ARG PNPM_NETWORK_CONCURRENCY=4

RUN corepack enable && corepack prepare pnpm@11.2.2 --activate \
    && pnpm config set registry "${NPM_REGISTRY}" \
    && pnpm config set fetch-timeout "${PNPM_FETCH_TIMEOUT}" \
    && pnpm config set fetch-retries "${PNPM_FETCH_RETRIES}" \
    && pnpm config set fetch-retry-mintimeout 20000 \
    && pnpm config set fetch-retry-maxtimeout 120000 \
    && pnpm config set network-concurrency "${PNPM_NETWORK_CONCURRENCY}"

ARG VITE_GLOB_API_URL=/api
ENV VITE_GLOB_API_URL=${VITE_GLOB_API_URL}

COPY . .
RUN pnpm install --frozen-lockfile
RUN pnpm -F @vben/web-antd run build

FROM nginx:1.27-alpine AS runtime
COPY apps/web-antd/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/apps/web-antd/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://localhost/ || exit 1
