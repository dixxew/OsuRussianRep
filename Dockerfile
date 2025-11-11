# ---------------------------
# STAGE 1: Build
# ---------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости
COPY OsuRussianRep/OsuRussianRep.csproj OsuRussianRep/
RUN dotnet restore OsuRussianRep/OsuRussianRep.csproj

# Копируем весь остальной код
COPY . .

# Паблишим
WORKDIR /src/OsuRussianRep
RUN dotnet publish -c Release -o /app/publish

# ---------------------------
# STAGE 2: Runtime
# ---------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

COPY start.sh /app/start.sh
RUN chmod +x /app/start.sh

# Открываем порт
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["/bin/bash", "/app/start.sh"]
