@echo off
echo Iniciando los 4 nodos DiskNode...

start dotnet run --project TecMFS.DiskNode -- 0
start dotnet run --project TecMFS.DiskNode -- 1
start dotnet run --project TecMFS.DiskNode -- 2
start dotnet run --project TecMFS.DiskNode -- 3
start dotnet run --project TecMFS.Controller

echo Todos los nodos han sido lanzados en ventanas separadas.
pause
