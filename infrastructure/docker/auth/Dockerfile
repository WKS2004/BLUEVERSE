# syntax=docker/dockerfile:1

FROM dhi.io/dotnet:10-sdk-alpine AS build

ARG PROJECT_NAME=Blueverse.Auth
WORKDIR /src

COPY services/auth/*.csproj ./

RUN --mount=type=cache,id=blueverse-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "${PROJECT_NAME}.csproj"
COPY services/auth/ ./
RUN --mount=type=cache,id=blueverse-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "${PROJECT_NAME}.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish/ ./

ENTRYPOINT ["dotnet", "Blueverse.Auth.dll"]
