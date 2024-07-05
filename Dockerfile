# Use the .NET SDK image to perform the build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS deps
WORKDIR /app
COPY ./src/dig.sln ./
COPY ./src/dig/dig.csproj ./dig/
COPY ./src/server/server.csproj ./server/
RUN dotnet restore ./server/server.csproj
RUN dotnet restore ./dig/dig.csproj

FROM deps as sources
COPY ./src/ ./

FROM sources as build_dig
RUN dotnet publish ./dig/dig.csproj -c Release -o /app/dig/publish

FROM sources as build_server
RUN dotnet publish ./server/server.csproj -c Release -o /app/server/publish

# Stage to download and unzip the latest release from GitHub
FROM ubuntu:20.04 AS downloader
WORKDIR /app
RUN apt-get update && apt-get install -y curl unzip wget
RUN curl -s https://api.github.com/repos/Datalayer-Storage/chia-server-coin-cli/releases/latest \
    | grep "browser_download_url.*linux-x64.*zip" \
    | cut -d : -f 2,3 \
    | tr -d \" \
    | wget -O server_coin.zip -i -
RUN unzip server_coin.zip -d /app
RUN chmod +x /app/server_coin

# Final stage to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS dig_server
WORKDIR /app
COPY --from=build_server /app/server/publish/ ./
COPY --from=build_dig /app/dig/publish/ ./
COPY --from=downloader /app/ ./
CMD ["./dig.server"]
