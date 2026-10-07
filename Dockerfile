FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/ArtCommission.Domain/ArtCommission.Domain.csproj", "src/ArtCommission.Domain/"]
COPY ["src/ArtCommission.Application/ArtCommission.Application.csproj", "src/ArtCommission.Application/"]
COPY ["src/ArtCommission.Infrastructure/ArtCommission.Infrastructure.csproj", "src/ArtCommission.Infrastructure/"]
COPY ["src/ArtCommission.API/ArtCommission.API.csproj", "src/ArtCommission.API/"]
RUN dotnet restore "src/ArtCommission.API/ArtCommission.API.csproj"

COPY . .
RUN dotnet publish "src/ArtCommission.API/ArtCommission.API.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "ArtCommission.API.dll"]
