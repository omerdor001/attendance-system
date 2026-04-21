FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    libglib2.0-0 \
    libx11-6 \
    libxext6 \
    libxrender1 \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["AttendanceSystem.API/AttendanceSystem.API.csproj", "AttendanceSystem.API/"]
COPY ["AttendanceSystem.Core/AttendanceSystem.Core.csproj", "AttendanceSystem.Core/"]
COPY ["AttendanceSystem.Infrastructure/AttendanceSystem.Infrastructure.csproj", "AttendanceSystem.Infrastructure/"]
RUN dotnet restore "AttendanceSystem.API/AttendanceSystem.API.csproj"
COPY . .
WORKDIR "/src/AttendanceSystem.API"
RUN dotnet build "AttendanceSystem.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AttendanceSystem.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AttendanceSystem.API.dll"]
