FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/Honua.Server.AlertReceiver/Honua.Server.AlertReceiver.csproj", "Honua.Server.AlertReceiver/"]
RUN dotnet restore "Honua.Server.AlertReceiver/Honua.Server.AlertReceiver.csproj"

COPY src/Honua.Server.AlertReceiver/ Honua.Server.AlertReceiver/
WORKDIR "/src/Honua.Server.AlertReceiver"
RUN dotnet build "Honua.Server.AlertReceiver.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Honua.Server.AlertReceiver.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Honua.Server.AlertReceiver.dll"]
