@echo off
echo Starting High-Accuracy UI-to-RC File Generator...
dotnet run --project "%~dp0UiToRcGenerator\UiToRcGenerator.csproj"
pause
