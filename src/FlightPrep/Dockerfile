FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy project files first for layer-cached restore
COPY FlightPrep.Domain/FlightPrep.Domain.csproj FlightPrep.Domain/
COPY FlightPrep.Infrastructure/FlightPrep.Infrastructure.csproj FlightPrep.Infrastructure/
COPY FlightPrep/FlightPrep.csproj FlightPrep/
RUN dotnet restore FlightPrep/FlightPrep.csproj
COPY . .
RUN dotnet publish FlightPrep/FlightPrep.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FlightPrep.dll"]
