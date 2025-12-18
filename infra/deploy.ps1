$ErrorActionPreference = "Stop"

param(
    [string]$Environment = "dev",
    [string]$Project = "FabricServicePrincipalProfile.csproj"
)

Write-Host "Environment: $Environment"

# Build and run the project with env arg; assumes env vars/Key Vault are already configured.
dotnet restore
dotnet build $Project --configuration Release
dotnet run --project $Project -- --environment $Environment
