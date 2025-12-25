Write-Host "=== Сборка Notification System ===" -ForegroundColor Green

Write-Host "Восстановление зависимостей..." -ForegroundColor Yellow
dotnet restore

Write-Host "Сборка проектов..." -ForegroundColor Yellow
dotnet build --configuration Release --no-restore

Write-Host "=== Сборка завершена ===" -ForegroundColor Green