FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Notification.Contracts/Notification.Contracts.csproj", "Notification.Contracts/"]
COPY ["Notification.EmailService/Notification.EmailService.csproj", "Notification.EmailService/"]
COPY ["Notification.SmsService/Notification.SmsService.csproj", "Notification.SmsService/"]
RUN dotnet restore "Notification.SmsService/Notification.SmsService.csproj"

COPY . .

WORKDIR "/src/Notification.SmsService"
RUN dotnet build "Notification.SmsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Notification.SmsService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.SmsService.dll"]