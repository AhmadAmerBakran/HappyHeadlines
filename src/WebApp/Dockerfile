FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/WebApp/WebApp.csproj", "src/WebApp/"]
COPY ["src/Shared/Observability/HappyHeadlines.Observability.csproj", "src/Shared/Observability/"]
RUN dotnet restore "src/WebApp/WebApp.csproj"

COPY . .
WORKDIR "/src/src/WebApp"
RUN dotnet publish "WebApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WebApp.dll"]
