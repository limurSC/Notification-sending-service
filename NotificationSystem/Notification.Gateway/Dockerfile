FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Notification.Contracts/Notification.Contracts.csproj", "Notification.Contracts/"]
COPY ["Notification.Gateway/Notification.Gateway.csproj", "Notification.Gateway/"]
RUN dotnet restore "Notification.Gateway/Notification.Gateway.csproj"

COPY . .

WORKDIR "/src/Notification.Gateway"
RUN dotnet build "Notification.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Notification.Gateway.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.Gateway.dll"]