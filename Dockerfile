FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Landing.Api/Landing.Api.csproj src/Landing.Api/
RUN dotnet restore src/Landing.Api/Landing.Api.csproj
COPY src/Landing.Api/ src/Landing.Api/
RUN dotnet publish src/Landing.Api/Landing.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Landing.Api.dll"]
