# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /app

RUN corepack enable && corepack prepare pnpm@11.2.2 --activate

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
