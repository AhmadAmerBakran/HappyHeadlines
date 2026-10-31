FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SubscriberService/SubscriberService.csproj", "src/SubscriberService/"]
COPY ["src/Shared/Observability/HappyHeadlines.Observability.csproj", "src/Shared/Observability/"]
RUN dotnet restore "src/SubscriberService/SubscriberService.csproj"
COPY . .
WORKDIR "/src/src/SubscriberService"
RUN dotnet publish "SubscriberService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SubscriberService.dll"]
