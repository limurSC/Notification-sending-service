Write-Host "=== Запуск Notification System в Docker ===" -ForegroundColor Green

docker-compose down

docker-compose up --build

Write-Host "=== Система запущена ===" -ForegroundColor Green
Write-Host "Gateway: http://localhost:8080" -ForegroundColor Cyan
Write-Host "Swagger: http://localhost:8080/swagger" -ForegroundColor Cyan
Write-Host "RabbitMQ: http://localhost:15672 (guest/guest)" -ForegroundColor Cyan