# Використовуємо офіційний образ .NET SDK для складання
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо файли .csproj для відновлення залежностей
COPY ["DistributedSolver.sln", "."]
COPY ["src/DistributedSolver.Api/DistributedSolver.Api.csproj", "src/DistributedSolver.Api/"]
COPY ["src/DistributedSolver.Domain/DistributedSolver.Domain.csproj", "src/DistributedSolver.Domain/"]
COPY ["src/DistributedSolver.Infrastructure/DistributedSolver.Infrastructure.csproj", "src/DistributedSolver.Infrastructure/"]

# Відновлення залежностей
RUN dotnet restore

# Копіюємо весь вихідний код
COPY . .
WORKDIR /src/src/DistributedSolver.Api

# Публікуємо застосунок
RUN dotnet publish -c Release -o /app/publish

# Використовуємо офіційний образ ASP.NET runtime для запуску
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Запуск застосунку
ENTRYPOINT ["dotnet", "DistributedSolver.Api.dll"]