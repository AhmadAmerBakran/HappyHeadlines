FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DraftService/DraftService.csproj", "src/DraftService/"]
COPY ["src/Shared/Observability/HappyHeadlines.Observability.csproj", "src/Shared/Observability/"]
RUN dotnet restore "src/DraftService/DraftService.csproj"
COPY . .
WORKDIR "/src/src/DraftService"
RUN dotnet publish "DraftService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DraftService.dll"]
