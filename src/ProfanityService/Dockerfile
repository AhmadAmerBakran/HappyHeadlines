FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ProfanityService/ProfanityService.csproj", "src/ProfanityService/"]
RUN dotnet restore "src/ProfanityService/ProfanityService.csproj"

COPY . .
WORKDIR "/src/src/ProfanityService"
RUN dotnet publish "ProfanityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProfanityService.dll"]
