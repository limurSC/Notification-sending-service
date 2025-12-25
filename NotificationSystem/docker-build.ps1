Write-Host "=== Сборка Docker образов ===" -ForegroundColor Green

docker-compose build

Write-Host "=== Docker образы собраны ===" -ForegroundColor Green
Write-Host "Запуск: docker-compose up" -ForegroundColor Cyan