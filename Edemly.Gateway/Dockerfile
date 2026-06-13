FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Edemly.Gateway/Edemly.Gateway.csproj Edemly.Gateway/
RUN dotnet restore Edemly.Gateway/Edemly.Gateway.csproj

COPY Edemly.Gateway/ Edemly.Gateway/
WORKDIR /src/Edemly.Gateway
RUN dotnet publish Edemly.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Edemly.Gateway.dll"]
