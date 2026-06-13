FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Edemly.Contracts/Edemly.Contracts.csproj Edemly.Contracts/
COPY Edemly.Server/Edemly.Server.csproj Edemly.Server/
RUN dotnet restore Edemly.Server/Edemly.Server.csproj

COPY Edemly.Contracts/ Edemly.Contracts/
COPY Edemly.Server/ Edemly.Server/
WORKDIR /src/Edemly.Server
RUN dotnet publish Edemly.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV PORT=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Edemly.Server.dll"]
