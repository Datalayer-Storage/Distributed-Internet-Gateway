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

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS dig_server
WORKDIR /app
COPY --from=build_server /app/server/publish/ ./
COPY --from=build_dig /app/dig/publish/ ./
CMD ["./dig.server"]
