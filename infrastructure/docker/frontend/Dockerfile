# syntax=docker/dockerfile:1

FROM dhi.io/node:24-debian13-dev AS build

WORKDIR /app

COPY apps/web/package*.json ./
RUN npm ci

COPY apps/web/ ./
RUN npm run build

FROM dhi.io/nginx:1.30-alpine AS runtime

COPY infrastructure/docker/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
