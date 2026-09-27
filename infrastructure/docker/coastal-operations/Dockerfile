# syntax=docker/dockerfile:1

FROM dhi.io/dotnet:10-sdk-alpine AS build

ARG PROJECT_NAME=Blueverse.CoastalOperations
WORKDIR /src

COPY services/coastal-operations/ ./

RUN dotnet restore "${PROJECT_NAME}.csproj"
RUN mkdir -p /var/lib/coastal-operations/evidence \
    && chown 65532:65532 /var/lib/coastal-operations/evidence \
    && chmod 0750 /var/lib/coastal-operations/evidence
RUN dotnet publish "${PROJECT_NAME}.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM dhi.io/aspnetcore:10-alpine AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish/ ./
COPY --from=build --chown=65532:65532 \
    /var/lib/coastal-operations/evidence/ /var/lib/coastal-operations/evidence/

ENTRYPOINT ["dotnet", "Blueverse.CoastalOperations.dll"]
