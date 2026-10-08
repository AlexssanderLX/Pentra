# syntax=docker/dockerfile:1

# ---- Build stage -----------------------------------------------------------
# The Tailwind CSS is pre-built and committed (wwwroot/css/app.css), so the
# container build needs only the .NET SDK — no Node toolchain.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files for better layer caching.
COPY Pentra.slnx ./
COPY src/Pentra.Domain/Pentra.Domain.csproj src/Pentra.Domain/
COPY src/Pentra.Application/Pentra.Application.csproj src/Pentra.Application/
COPY src/Pentra.Infrastructure/Pentra.Infrastructure.csproj src/Pentra.Infrastructure/
COPY src/Pentra.Web/Pentra.Web.csproj src/Pentra.Web/
COPY tests/Pentra.Tests/Pentra.Tests.csproj tests/Pentra.Tests/
RUN dotnet restore src/Pentra.Web/Pentra.Web.csproj

# Copy the rest and publish.
COPY . .
RUN dotnet publish src/Pentra.Web/Pentra.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Writable data directory for the SQLite database (mounted as a volume).
# Owned by the non-root 'app' user that the base image provides.
RUN mkdir -p /app/data && chown -R app:app /app/data

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Pentra="Data Source=/app/data/pentra.db"

EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "Pentra.Web.dll"]
