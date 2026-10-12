FROM dhi.io/node:24-debian13-dev
WORKDIR /app
COPY services/coastal-planner/fake-it3091/server.mjs ./server.mjs
ENV PORT=8080
EXPOSE 8080
USER node
ENTRYPOINT ["node", "server.mjs"]
