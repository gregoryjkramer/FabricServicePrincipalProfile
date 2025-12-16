using System;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Microsoft.Extensions.Configuration;

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
                DeploymentManager.Deploy_Hybrid_Solution("Contoso");
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
