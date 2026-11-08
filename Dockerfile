# syntax=docker/dockerfile:1

# --- Stage 1: build ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DevOpsLab.sln ./
COPY src/DevOpsLab.Domain/DevOpsLab.Domain.csproj                 src/DevOpsLab.Domain/
COPY src/DevOpsLab.Application/DevOpsLab.Application.csproj       src/DevOpsLab.Application/
COPY src/DevOpsLab.Infrastructure/DevOpsLab.Infrastructure.csproj src/DevOpsLab.Infrastructure/
COPY src/DevOpsLab.WebApi/DevOpsLab.WebApi.csproj                 src/DevOpsLab.WebApi/

RUN dotnet restore src/DevOpsLab.WebApi/DevOpsLab.WebApi.csproj

COPY src/ src/

RUN dotnet publish src/DevOpsLab.WebApi/DevOpsLab.WebApi.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# --- Stage 2: runtime --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-extra AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

COPY --from=build /app/publish .

USER app

ENTRYPOINT ["dotnet", "DevOpsLab.WebApi.dll"]