# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Stage 1: Build Node.js frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend
COPY paymentgateway.client/package*.json ./
RUN npm ci
COPY paymentgateway.client/ ./
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["PaymentGateway.Server/PaymentGateway.Server.csproj", "PaymentGateway.Server/"]
RUN dotnet restore "./PaymentGateway.Server/PaymentGateway.Server.csproj"
COPY . .
WORKDIR "/src/PaymentGateway.Server"
RUN dotnet build "./PaymentGateway.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish .NET backend
FROM backend-build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./PaymentGateway.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Copy frontend build to backend wwwroot and create final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=frontend-build /app/frontend/build/client ./wwwroot
EXPOSE 8080
EXPOSE 8081
VOLUME ["/app/local-storage-service"]
ENTRYPOINT ["dotnet", "PaymentGateway.Server.dll"]