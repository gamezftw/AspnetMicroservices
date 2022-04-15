FROM mcr.microsoft.com/dotnet/aspnet:6.0-focal AS base
WORKDIR /app
EXPOSE 80

ENV ASPNETCORE_URLS=http://+:80

FROM mcr.microsoft.com/dotnet/sdk:6.0-focal AS build
WORKDIR /src
COPY ["src/ApiGateways/Shopping.Aggregator/Shopping.Aggergator.csproj", "src/ApiGateways/Shopping.Aggregator/"]
RUN dotnet restore "src/ApiGateways/Shopping.Aggregator/Shopping.Aggergator.csproj"
COPY . .
WORKDIR "/src/src/ApiGateways/Shopping.Aggregator"
RUN dotnet build "Shopping.Aggergator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Shopping.Aggergator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Shopping.Aggergator.dll"]
