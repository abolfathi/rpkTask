# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY OrderManagement.sln ./
COPY src/OrderManagement.API/OrderManagement.API.csproj src/OrderManagement.API/
COPY src/OrderManagement.Application/OrderManagement.Application.csproj src/OrderManagement.Application/
COPY src/OrderManagement.Domain/OrderManagement.Domain.csproj src/OrderManagement.Domain/
COPY src/OrderManagement.Infrastructure/OrderManagement.Infrastructure.csproj src/OrderManagement.Infrastructure/

RUN dotnet restore src/OrderManagement.API/OrderManagement.API.csproj

FROM restore AS build
COPY src/ src/
RUN dotnet publish src/OrderManagement.API/OrderManagement.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM restore AS migrate
COPY src/ src/
RUN dotnet tool install --global dotnet-ef --version 10.0.13
ENV PATH="${PATH}:/root/.dotnet/tools"
WORKDIR /src
ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--project", "src/OrderManagement.Infrastructure/OrderManagement.Infrastructure.csproj", \
    "--startup-project", "src/OrderManagement.API/OrderManagement.API.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OrderManagement.API.dll"]
