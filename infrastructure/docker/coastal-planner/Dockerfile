# syntax=docker/dockerfile:1

FROM dhi.io/dotnet:10-sdk-alpine AS build

ARG PROJECT_NAME=Blueverse.CoastalPlanner
WORKDIR /src

COPY services/coastal-planner/*.csproj ./

RUN --mount=type=cache,id=blueverse-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore "${PROJECT_NAME}.csproj"
COPY services/coastal-planner/ ./
RUN --mount=type=cache,id=blueverse-nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "${PROJECT_NAME}.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM dhi.io/aspnetcore:10-alpine AS runtime

ARG PROJECT_NAME=Blueverse.CoastalPlanner
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish/ ./

ENTRYPOINT ["dotnet", "Blueverse.CoastalPlanner.dll"]
