FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ArticleService/ArticleService.csproj", "src/ArticleService/"]
RUN dotnet restore "src/ArticleService/ArticleService.csproj"

COPY . .
WORKDIR "/src/src/ArticleService"
RUN dotnet publish "ArticleService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ArticleService.dll"]
