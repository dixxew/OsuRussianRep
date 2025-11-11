#!/bin/bash
set -e

echo "⚙️ Генерация appsettings.json..."

cat > /app/appsettings.json <<EOF
{
  "IrcConnection": {
    "Nickname": "${IrcConnection__Nickname}",
    "Server": "${IrcConnection__Server}",
    "Port": ${IrcConnection__Port:-6667},
    "Password": "${IrcConnection__Password}",
    "Channel": "${IrcConnection__Channel}"
  },
  "OsuApi": {
    "ClientId": "${OsuApi__ClientId}",
    "ClientSecret": "${OsuApi__ClientSecret}"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "${ConnectionStrings__DefaultConnection}"
  }
}
EOF

echo "✅ appsettings.json создан:"
cat /app/appsettings.json
echo "🚀 Запуск приложения..."
exec dotnet OsuRussianRep.dll
