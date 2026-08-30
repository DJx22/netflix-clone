#!/usr/bin/env bash

# -----------------------------------------------------------------------------
# SYNOPSIS
#   Scaffolds a new microservice with the
#   Api/Application/Domain/Infrastructure/Tests layout and inward-pointing
#   project references.
#
# USAGE
#   ./new-service.sh Identity
#   ./new-service.sh Catalog
#   ./new-service.sh Subscription
# -----------------------------------------------------------------------------

set -euo pipefail

# -----------------------------------------------------------------------------
# Helpers
# -----------------------------------------------------------------------------

error() {
    echo "Error: $*" >&2
    exit 1
}

warning() {
    echo "Warning: $*" >&2
}

info() {
    echo -e "\033[36m$*\033[0m"
}

success() {
    echo -e "\033[32m$*\033[0m"
}

yellow() {
    echo -e "\033[33m$*\033[0m"
}

# -----------------------------------------------------------------------------
# Parameters
# -----------------------------------------------------------------------------

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <ServiceName>"
    echo
    echo "Example:"
    echo "  $0 Identity"
    exit 1
fi

ServiceName="$1"

if [[ -z "$ServiceName" ]]; then
    error "ServiceName cannot be empty."
fi

# -----------------------------------------------------------------------------
# Validate .NET SDK
# -----------------------------------------------------------------------------

if ! command -v dotnet >/dev/null 2>&1; then
    error ".NET SDK not found on PATH. Confirm the .NET 10 SDK is installed (prerequisites checklist) before running this."
fi

sdkVersion="$(dotnet --version)"

if [[ "$sdkVersion" != 10.* ]]; then
    warning "dotnet --version reports $sdkVersion, not 10.x. This project targets .NET 10 — the script will still run, but double-check before Phase 1."
fi

# -----------------------------------------------------------------------------
# Paths
# -----------------------------------------------------------------------------

folderName="$(echo "$ServiceName" | tr '[:upper:]' '[:lower:]')-service"
root="services/$folderName"

if [[ -e "$root" ]]; then
    error "services/$folderName already exists. Pick a different name or delete it first."
fi

info "Scaffolding $ServiceName into $root (SDK $sdkVersion) ..."

mkdir -p "$root"

pushd "$root" >/dev/null

# -----------------------------------------------------------------------------
# Solution
# One per service — each is a standalone Web API per §1
# -----------------------------------------------------------------------------

dotnet new sln \
    -n "$ServiceName" \
    >/dev/null

# -----------------------------------------------------------------------------
# Projects
# -----------------------------------------------------------------------------

# Domain:
# Pure C#, zero framework references — classlib is correct,
# never webapi/worker.
dotnet new classlib \
    -n "$ServiceName.Domain" \
    -o "$ServiceName.Domain" \
    -f net10.0 \
    >/dev/null

# Application:
# Use cases, DTOs, validators, interfaces.
dotnet new classlib \
    -n "$ServiceName.Application" \
    -o "$ServiceName.Application" \
    -f net10.0 \
    >/dev/null

# Infrastructure:
# EF Core / Dapper, external clients, repository implementations.
dotnet new classlib \
    -n "$ServiceName.Infrastructure" \
    -o "$ServiceName.Infrastructure" \
    -f net10.0 \
    >/dev/null

# Api:
# Controllers, not minimal APIs.
dotnet new webapi \
    -n "$ServiceName.Api" \
    -o "$ServiceName.Api" \
    -f net10.0 \
    --use-controllers \
    >/dev/null

# Tests:
# xUnit, one project for now.
dotnet new xunit \
    -n "$ServiceName.Tests" \
    -o "$ServiceName.Tests" \
    -f net10.0 \
    >/dev/null

# -----------------------------------------------------------------------------
# Project paths
# -----------------------------------------------------------------------------

domain="$ServiceName.Domain/$ServiceName.Domain.csproj"
application="$ServiceName.Application/$ServiceName.Application.csproj"
infrastructure="$ServiceName.Infrastructure/$ServiceName.Infrastructure.csproj"
api="$ServiceName.Api/$ServiceName.Api.csproj"
tests="$ServiceName.Tests/$ServiceName.Tests.csproj"

# -----------------------------------------------------------------------------
# Wire references
#
# Dependencies point inward (§2, §5).
# Domain gets no project references.
# -----------------------------------------------------------------------------

dotnet add "$application" reference "$domain"

dotnet add "$infrastructure" reference "$domain"
dotnet add "$infrastructure" reference "$application"

dotnet add "$api" reference "$application"
dotnet add "$api" reference "$infrastructure"

dotnet add "$tests" reference "$domain"
dotnet add "$tests" reference "$application"
dotnet add "$tests" reference "$infrastructure"

# -----------------------------------------------------------------------------
# Add projects to solution
# -----------------------------------------------------------------------------

dotnet sln add \
    "$domain" \
    "$application" \
    "$infrastructure" \
    "$api" \
    "$tests"

# -----------------------------------------------------------------------------
# Strip template noise
# -----------------------------------------------------------------------------

rm -f "$ServiceName.Domain/Class1.cs"
rm -f "$ServiceName.Application/Class1.cs"
rm -f "$ServiceName.Infrastructure/Class1.cs"
rm -f "$ServiceName.Api/WeatherForecast.cs"
rm -f "$ServiceName.Api/Controllers/WeatherForecastController.cs"

# Remove Controllers directory if it is now empty.
rmdir "$ServiceName.Api/Controllers" 2>/dev/null || true

# -----------------------------------------------------------------------------
# DependencyInjection.cs stub
#
# One per Api project, §7:
# not scattered across Program.cs.
# -----------------------------------------------------------------------------

cat > "$ServiceName.Api/DependencyInjection.cs" <<EOF
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace $ServiceName.Api;

public static class DependencyInjection
{
    public static IServiceCollection Add${ServiceName}Services(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register repositories, application services, validators, etc. here.
        // Bind config sections with IOptions<T> here — never read
        // IConfiguration[""] outside this file.
        return services;
    }
}
EOF

popd >/dev/null

# -----------------------------------------------------------------------------
# Done
# -----------------------------------------------------------------------------

success "Done. $root has 5 projects, wired per csharp-coding-standard.md."

yellow "Not done here on purpose: NuGet packages (EF Core vs MongoDB.Driver vs MediatR differ per service — add those in Phase 1), and the actual Program.cs wiring."