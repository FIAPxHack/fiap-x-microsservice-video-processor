# Etapa 1 -> Base
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Instalar FFmpeg
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*

# Etapa 2 -> Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Core/FiapXVideoProcessor.Domain/FiapXVideoProcessor.Domain.csproj", "Core/FiapXVideoProcessor.Domain/"]
COPY ["Core/FiapXVideoProcessor.Application/FiapXVideoProcessor.Application.csproj", "Core/FiapXVideoProcessor.Application/"]
COPY ["Infrastructure/FiapXVideoProcessor.Infrastructure/FiapXVideoProcessor.Infrastructure.csproj", "Infrastructure/FiapXVideoProcessor.Infrastructure/"]
COPY ["Worker/FiapXVideoProcessor.Worker/FiapXVideoProcessor.Worker.csproj", "Worker/FiapXVideoProcessor.Worker/"]

WORKDIR /src/Worker/FiapXVideoProcessor.Worker
RUN dotnet restore

WORKDIR /src
COPY ["Core/", "Core/"]
COPY ["Infrastructure/", "Infrastructure/"]
COPY ["Worker/", "Worker/"]

WORKDIR /src/Worker/FiapXVideoProcessor.Worker
RUN dotnet publish -c Release -o /app/publish

# Etapa 3 -> Final
FROM base AS final
RUN adduser --disabled-password --gecos "" appuser
USER appuser
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FiapXVideoProcessor.Worker.dll"]