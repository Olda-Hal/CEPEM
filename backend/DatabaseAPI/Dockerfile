# Use the official .NET 8 SDK image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Build context is backend/ so the shared source linked from the csproj
# (../Shared/ForwardedHeadersSetup.cs) is available inside the image.
WORKDIR /src

# Copy project file and shared code, then restore dependencies
COPY ["DatabaseAPI/DatabaseAPI.csproj", "DatabaseAPI/"]
COPY Shared/ Shared/
RUN dotnet restore "DatabaseAPI/DatabaseAPI.csproj"

# Copy the rest of the project source code
COPY DatabaseAPI/ DatabaseAPI/
WORKDIR /src/DatabaseAPI

# Build the application
RUN dotnet build "DatabaseAPI.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DatabaseAPI.csproj" -c Release -o /app/publish

# Use the official .NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install wait-for-it plus the document text/OCR toolchain (PDF text layer,
# scanned PDFs and images). The service degrades to "Unavailable" when a
# configured tool is missing, so this is not a hard runtime requirement.
RUN apt-get update && apt-get install -y --no-install-recommends \
        wget \
        gosu \
        poppler-utils \
        tesseract-ocr \
        tesseract-ocr-ces \
        tesseract-ocr-eng \
        tesseract-ocr-nld && \
    wget -O /wait-for-it.sh https://raw.githubusercontent.com/vishnubob/wait-for-it/master/wait-for-it.sh && \
    chmod +x /wait-for-it.sh && \
    rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
COPY DatabaseAPI/docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# Expose port
EXPOSE 80
EXPOSE 5001

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
