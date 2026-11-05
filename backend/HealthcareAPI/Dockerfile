# Use the official .NET 8 SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Build context is backend/ so the shared source linked from the csproj
# (../Shared/ForwardedHeadersSetup.cs) is available inside the image.
WORKDIR /app

# Copy project file and shared code, then restore dependencies
COPY HealthcareAPI/HealthcareAPI.csproj HealthcareAPI/
COPY Shared/ Shared/
RUN dotnet restore HealthcareAPI/HealthcareAPI.csproj

# Copy everything else and build
COPY HealthcareAPI/ HealthcareAPI/
WORKDIR /app/HealthcareAPI
RUN dotnet publish -c Release -o /app/out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install curl for the compose healthcheck (the runtime image ships without it)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

# Run the API as the unprivileged user shipped with the .NET image.
USER app

# Expose port
EXPOSE 80

ENTRYPOINT ["dotnet", "HealthcareAPI.dll"]
