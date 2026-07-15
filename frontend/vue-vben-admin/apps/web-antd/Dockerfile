# syntax=docker/dockerfile:1

ARG NODE_IMAGE=node:24-alpine
ARG NGINX_IMAGE=nginx:1.27-alpine

FROM ${NODE_IMAGE} AS build
WORKDIR /app

ENV CI=true
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0
ENV NODE_OPTIONS=--max-old-space-size=4096

ARG NPM_REGISTRY=https://registry.npmmirror.com
ARG PNPM_FETCH_TIMEOUT=600000
ARG PNPM_FETCH_RETRIES=5
ARG PNPM_NETWORK_CONCURRENCY=4

ENV COREPACK_NPM_REGISTRY=${NPM_REGISTRY}
ENV COREPACK_DEFAULT_TO_LATEST=0

RUN apk add --no-cache git \
    && corepack enable && corepack prepare pnpm@11.2.2 --activate \
    && pnpm config set registry "${NPM_REGISTRY}" \
    && pnpm config set fetch-timeout "${PNPM_FETCH_TIMEOUT}" \
    && pnpm config set fetch-retries "${PNPM_FETCH_RETRIES}" \
    && pnpm config set fetch-retry-mintimeout 20000 \
    && pnpm config set fetch-retry-maxtimeout 120000 \
    && pnpm config set network-concurrency "${PNPM_NETWORK_CONCURRENCY}"

ARG VITE_GLOB_API_URL=/api
ENV VITE_GLOB_API_URL=${VITE_GLOB_API_URL}

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
RUN --mount=type=cache,id=miniadmin-pnpm-store,target=/pnpm/store \
    pnpm config set store-dir /pnpm/store \
    && pnpm fetch --frozen-lockfile

COPY . .
RUN --mount=type=cache,id=miniadmin-pnpm-store,target=/pnpm/store \
    pnpm config set store-dir /pnpm/store \
    && pnpm install --frozen-lockfile --offline
RUN pnpm -F @vben/web-antd run build

FROM ${NGINX_IMAGE} AS runtime
COPY apps/web-antd/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/apps/web-antd/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://localhost/ || exit 1
