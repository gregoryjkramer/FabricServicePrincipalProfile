using System;
using Microsoft.Extensions.Configuration;

public static class AppSettings {

  public const string FabricRestApiBaseUrl = "https://api.fabric.microsoft.com/v1";
  public const string PowerBiRestApiBaseUrl = "https://api.powerbi.com";
  public const string OneLakeBaseUrl = "https://onelake.dfs.fabric.microsoft.com";

  public const string LocalPbixFolder = @"..\..\..\PBIX\";
  public const string LocalWebPageTemplatesFolder = @"..\..\..\WebPageTemplates\";
  public const string LocalWebPagesFolder = @"..\..\..\WebPages\";
  public const string LocalTemplateFilesRoot = @"..\..\..\ItemDefinitions\ItemDefinitionTemplateFiles\";
  public const string LocalItemTemplatesFolder = @"..\..\..\ItemDefinitions\ItemDefinitionTemplateFolders\";

  private static bool isInitialized;

  public static string FabricCapacityId { get; private set; } = string.Empty;
  public static AppAuthenticationMode AuthenticationMode { get; private set; } = AppAuthenticationMode.ServicePrincipalAuth;
  public static string ServicePrincipalAuthTenantId { get; private set; } = string.Empty;
  public static string ServicePrincipalAuthClientId { get; private set; } = string.Empty;
  public static string ServicePrincipalAuthClientSecret { get; private set; } = string.Empty;
  public static string ServicePrincipalObjectId { get; private set; } = string.Empty;
  public static string ServicePrincipalProfileId { get; private set; } = string.Empty;
  public static string AdminUserId { get; private set; } = string.Empty;
  public static string UserAuthClientId { get; private set; } = string.Empty;
  public static string UserAuthRedirectUri { get; private set; } = "http://localhost";

  public static void Initialize(IConfiguration configuration) {

    if (isInitialized) {
      return;
    }

    FabricCapacityId = GetRequired(configuration, "AppSettings:FabricCapacityId");
    ServicePrincipalAuthTenantId = GetRequired(configuration, "AppSettings:ServicePrincipalAuthTenantId");
    ServicePrincipalAuthClientId = GetRequired(configuration, "AppSettings:ServicePrincipalAuthClientId");
    ServicePrincipalAuthClientSecret = GetRequired(configuration, "AppSettings:ServicePrincipalAuthClientSecret");
    ServicePrincipalObjectId = GetRequired(configuration, "AppSettings:ServicePrincipalObjectId");
    ServicePrincipalProfileId = GetRequired(configuration, "AppSettings:ServicePrincipalProfileId");
    AdminUserId = GetRequired(configuration, "AppSettings:AdminUserId");

    var authMode = configuration["AppSettings:AuthenticationMode"];
    if (!string.IsNullOrWhiteSpace(authMode) &&
        Enum.TryParse<AppAuthenticationMode>(authMode, true, out var parsedMode)) {
      AuthenticationMode = parsedMode;
    }

    UserAuthClientId = configuration["AppSettings:UserAuthClientId"] ?? UserAuthClientId;
    UserAuthRedirectUri = configuration["AppSettings:UserAuthRedirectUri"] ?? UserAuthRedirectUri;

    isInitialized = true;
  }

  public static void EnsureInitialized()
  {
    if (!isInitialized)
    {
      throw new InvalidOperationException(
        "AppSettings.Initialize(configuration) must be called before use.");
    }
  }

  private static string GetRequired(IConfiguration configuration, string key) {

    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value)) {
      throw new InvalidOperationException($"Configuration value '{key}' is missing. Supply it in appsettings or Key Vault.");
    }

    return value;
  }

}


