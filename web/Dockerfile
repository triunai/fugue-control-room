# syntax=docker/dockerfile:1
# Fugue Console: Angular build (docker configuration) served by nginx, /api proxied to the "api" compose service.
# Build context: web/.
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration docker

FROM nginxinc/nginx-unprivileged:1.27-alpine AS web
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
EXPOSE 8080
