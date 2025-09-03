#!/bin/bash

# Plugin Launcher Demo Script
echo "=== Plugin Launcher Demo ==="
echo

cd PluginLauncher

echo "1. Просмотр справки:"
dotnet run help
echo

echo "2. Просмотр текущей конфигурации:"
dotnet run config show
echo

echo "3. Настройка репозитория:"
dotnet run config set --owner example --repo plugins
echo

echo "4. Проверка обновленной конфигурации:"
dotnet run config show
echo

echo "5. Просмотр статуса (нет установленных плагинов):"
dotnet run status
echo

echo "6. Попытка просмотра списка плагинов (потребует доступ к реальному репозиторию):"
echo "dotnet run list"
echo

echo "=== Демонстрация завершена ==="
echo "Для реального использования настройте существующий GitHub репозиторий с releases."