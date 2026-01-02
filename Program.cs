using System;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace FabricServicePrincipalProfile
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(">>> ENTERED Main");

                // 1. Build base configuration (NO secrets here)
                var configBuilder = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddEnvironmentVariables();

                var preConfig = configBuilder.Build();
                Console.WriteLine(">>> Base configuration built");

                // 2. Load Key Vault EARLY
                var keyVaultUri = preConfig["KeyVault:Uri"];
                if (string.IsNullOrWhiteSpace(keyVaultUri))
                    throw new InvalidOperationException("KeyVault:Uri is missing");

                Console.WriteLine($">>> Using Key Vault: {keyVaultUri}");

                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ExcludeManagedIdentityCredential = true
                });

                configBuilder.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    credential);
                var configuration = configBuilder.Build();
                Console.WriteLine(">>> Key Vault configuration loaded");

                // 3. Initialize AppSettings
                AppSettings.Initialize(configuration);
                Console.WriteLine(">>> AppSettings initialized");

                // 4. Initialize Fabric API
                FabricRestApi.Initialize();
                Console.WriteLine(">>> FabricRestApi initialized");

                // 5. Run deployment
                Console.WriteLine(">>> Starting deployment");

                // Optional: environment selector via command-line arg "--environment <env>"
                var envArg = args
                    .SkipWhile(a => a != "--environment")
                    .Skip(1)
                    .FirstOrDefault();
                var environment = string.IsNullOrWhiteSpace(envArg) ? "dev" : envArg;
                Console.WriteLine($">>> Target environment: {environment}");

                // Load per-environment defaults from pipelines/env.json if present
                var envConfigPath = Path.Combine(AppContext.BaseDirectory, "pipelines", "env.json");
                string targetWorkspaceName = "Contoso";
                if (File.Exists(envConfigPath))
                {
                    var envJson = File.ReadAllText(envConfigPath);
                    using var doc = JsonDocument.Parse(envJson);
                    if (doc.RootElement.TryGetProperty(environment, out var envNode))
                    {
                        if (envNode.TryGetProperty("workspaceName", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                        {
                            targetWorkspaceName = nameProp.GetString() ?? targetWorkspaceName;
                        }
                    }
                }

                // Primary deploy: hybrid flow (lakehouse + notebook + semantic model + report)
                DeploymentManager.Deploy_Hybrid_Solution(targetWorkspaceName);

                // Optional: embed page generation (uncomment if desired)
                // DeploymentManager.Embed_Report_With_SPP(targetWorkspaceName);
                Console.WriteLine(">>> Deployment completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine(">>> FATAL ERROR");
                Console.WriteLine(ex.ToString());
            }

            // 6. Prevent instant exit
            Console.WriteLine(">>> Press ENTER to exit");
            Console.ReadLine();
        }
    }
}
