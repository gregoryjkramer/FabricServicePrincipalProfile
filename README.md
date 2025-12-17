# FabricServicePrincipalProfile
C# console application demonstrating use of service service profiles and Power BI embedding together with Fabric workspace items
.

## Running in VS Code

The repo includes a `.vscode` folder so you can debug the console app with the VS Code debugger instead of relying on Visual Studio solution settings.

1. Install the .NET 8 SDK and open the repository folder in VS Code.
2. Copy the template settings file (kept out of git) so the build can copy it:
   ```bash
   copy appsettings.template.json appsettings.json
   ```
3. Restore and build from the integrated terminal:
   ```bash
   dotnet restore
   dotnet build FabricServicePrincipalProfile.csproj
   ```
4. Press **F5** and select **.NET Launch (FabricServicePrincipalProfile)**. The launch profile runs `dotnet run --project FabricServicePrincipalProfile.csproj` from the repository root, keeping relative paths intact.
5. If you need environment variables (for secrets or overrides), create a `.env` file in the repo root. VS Code will load it automatically when you use the launch profile.

## Key Vault required keys

The app expects these configuration keys under `AppSettings:` (use Key Vault secret names that map to these, typically with `--` separators, for example `AppSettings--FabricCapacityId`):

- `FabricCapacityId`
- `ServicePrincipalAuthTenantId`
- `ServicePrincipalAuthClientId`
- `ServicePrincipalAuthClientSecret`
- `ServicePrincipalObjectId`
- `ServicePrincipalProfileId`
- `AdminUserId`
