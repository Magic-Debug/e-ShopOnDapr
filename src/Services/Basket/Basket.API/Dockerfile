#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
ARG NET_IMAGE=5.0-buster-slim
FROM mcr.microsoft.com/dotnet/aspnet:${NET_IMAGE} AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:${NET_IMAGE} AS build
WORKDIR /src

# Create this "restore-solution" section by running ./Create-DockerfileSolutionRestore.ps1, to optimize build cache reuse
COPY ["src/ApiGateways/Aggregators/Web.Shopping.HttpAggregator/Web.Shopping.HttpAggregator.csproj", "src/ApiGateways/Aggregators/Web.Shopping.HttpAggregator/"]
COPY ["src/BuildingBlocks/EventBus/EventBus.csproj", "src/BuildingBlocks/EventBus/"]
COPY ["src/BuildingBlocks/Healthchecks/Healthchecks.csproj", "src/BuildingBlocks/Healthchecks/"]
COPY ["src/Services/Basket/Basket.API/Basket.API.csproj", "src/Services/Basket/Basket.API/"]
COPY ["dapr-sdk-1.4.0/Dapr.Client/Dapr.Client.csproj", "dapr-sdk-1.4.0/Dapr.Client/"]
COPY ["dapr-sdk-1.4.0/Dapr.Actors/Dapr.Actors.csproj", "dapr-sdk-1.4.0/Dapr.Actors/"]
COPY ["dapr-sdk-1.4.0/Dapr.Extensions.Configuration/Dapr.Extensions.Configuration.csproj", "dapr-sdk-1.4.0/Dapr.Extensions.Configuration/"]
COPY ["dapr-sdk-1.4.0/Dapr.Actors.AspNetCore/Dapr.Actors.AspNetCore.csproj", "dapr-sdk-1.4.0/Dapr.Actors.AspNetCore/"]
COPY ["dapr-sdk-1.4.0/Dapr.AspNetCore/Dapr.AspNetCore.csproj", "dapr-sdk-1.4.0/Dapr.AspNetCore/"]
COPY ["src/Services/Catalog/Catalog.API/Catalog.API.csproj", "src/Services/Catalog/Catalog.API/"]
COPY ["src/Services/Identity/Identity.API/Identity.API.csproj", "src/Services/Identity/Identity.API/"]
COPY ["src/Services/Ordering/Ordering.API/Ordering.API.csproj", "src/Services/Ordering/Ordering.API/"]
COPY ["src/Services/Payment/Payment.API/Payment.API.csproj", "src/Services/Payment/Payment.API/"]
COPY ["src/Web/BlazorClient.Host/BlazorClient.Host.csproj", "src/Web/BlazorClient.Host/"]
COPY ["src/Web/BlazorClient/BlazorClient.csproj", "src/Web/BlazorClient/"]
COPY ["docker-compose.dcproj", "./"]
COPY ["NuGet.config", "./"]
COPY ["e-ShopOnDapr.sln", "./"]
RUN dotnet restore "e-ShopOnDapr.sln"

COPY . .
WORKDIR "/src/src/Services/Basket/Basket.API"
# RUN dotnet build "Basket.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --no-restore "Basket.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Basket.API.dll"]
