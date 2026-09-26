FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/NewsletterService/NewsletterService.csproj", "src/NewsletterService/"]
COPY ["src/Shared/Messaging/HappyHeadlines.Messaging.csproj", "src/Shared/Messaging/"]
COPY ["src/Shared/Observability/HappyHeadlines.Observability.csproj", "src/Shared/Observability/"]
RUN dotnet restore "src/NewsletterService/NewsletterService.csproj"

COPY . .
WORKDIR "/src/src/NewsletterService"
RUN dotnet publish "NewsletterService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NewsletterService.dll"]
