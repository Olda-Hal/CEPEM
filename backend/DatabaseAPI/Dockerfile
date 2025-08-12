# Use the official .NET 8 SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["DatabaseAPI.csproj", "./"]
RUN dotnet restore "DatabaseAPI.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "DatabaseAPI.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DatabaseAPI.csproj" -c Release -o /app/publish

# Use the official .NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install wait-for-it script for database readiness
RUN apt-get update && apt-get install -y wget && \
    wget -O /wait-for-it.sh https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh && \
    chmod +x /wait-for-it.sh

COPY --from=publish /app/publish .

# Expose port
EXPOSE 80
EXPOSE 5001

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["/wait-for-it.sh", "mysql:3306", "--timeout=60", "--", "dotnet", "DatabaseAPI.dll"]
