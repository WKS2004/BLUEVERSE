# syntax=docker/dockerfile:1

FROM dhi.io/dotnet:10-sdk-alpine AS build

ARG PROJECT_NAME=Blueverse.Auth
WORKDIR /src

COPY services/auth/ ./

RUN dotnet restore "${PROJECT_NAME}.csproj"
RUN dotnet publish "${PROJECT_NAME}.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM dhi.io/aspnetcore:10-alpine AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish/ ./

ENTRYPOINT ["dotnet", "Blueverse.Auth.dll"]
