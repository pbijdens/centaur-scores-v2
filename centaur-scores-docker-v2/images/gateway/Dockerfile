FROM node:24-alpine AS web-ui-build
WORKDIR /src
COPY centaur-scores-web-ui/package.json centaur-scores-web-ui/package-lock.json ./
RUN npm ci
COPY centaur-scores-web-ui/ ./
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM node:24-alpine AS scoring-build
WORKDIR /src
COPY centaur-scores-mobile-web-scoring/package.json centaur-scores-mobile-web-scoring/package-lock.json ./
RUN npm ci
COPY centaur-scores-mobile-web-scoring/ ./
RUN npm run build

FROM nginx:1.28-alpine AS runtime
COPY --from=web-ui-build /src/dist/ /usr/share/nginx/html/
COPY --from=scoring-build /src/dist/ /opt/centaur-scores/scoring/
COPY centaur-scores-docker-v2/images/gateway/default.conf.template /etc/nginx/templates/default.conf.template
COPY centaur-scores-docker-v2/images/gateway/05-install-scoring-app.sh /docker-entrypoint.d/05-install-scoring-app.sh
RUN chmod +x /docker-entrypoint.d/05-install-scoring-app.sh

EXPOSE 80
