FROM microsoft/dotnet:2.1-aspnetcore-runtime
WORKDIR /app
COPY . .
RUN apt-get update && apt-get install -y git vim
ENTRYPOINT ["dotnet", "Lykke.Service.DevCerts.dll"]
