

public class AppSettings {

  public const string FabricRestApiBaseUrl = "https://api.fabric.microsoft.com/v1";
  public const string PowerBiRestApiBaseUrl = "https://api.powerbi.com";
  public const string OneLakeBaseUrl = "https://onelake.dfs.fabric.microsoft.com";

  // TODO: configure capacity Id for Fabric-enabled capacity 
  public const string FabricCapacityId = "00000000-0000-0000-000000000000";

  // TODO: configure authentication mode
  public static AppAuthenticationMode AuthenticationMode = AppAuthenticationMode.ServicePrincipalAuth;

  // TODO: configure Entra Id application for service principal auth
  public const string ServicePrincipalAuthTenantId = "00000000-0000-0000-000000000000";
  public const string ServicePrincipalAuthClientId = "00000000-0000-0000-000000000000";
  public const string ServicePrincipalAuthClientSecret = "YOUR_SECRET_HERE";
  public const string ServicePrincipalObjectId = "00000000-0000-0000-000000000000";

  public const string ServicePrincipalProfileId = "00000000-0000-0000-000000000000";

  // TODO: configure object id of Entra Id user account of user running demo
  public const string AdminUserId = "00000000-0000-0000-000000000000"; // Entra ID for user

  // TODO: configure Entra Id application for user auth
  public const string UserAuthClientId = "Add GUID for App Id if using user auth";
  public const string UserAuthRedirectUri = "http://localhost";

  // paths to folders inside this project to read and write files
  public const string LocalPbixFolder = @"..\..\..\PBIX\";
  public const string LocalWebPageTemplatesFolder = @"..\..\..\WebPageTemplates\";
  public const string LocalWebPagesFolder = @"..\..\..\WebPages\";
  public const string LocalTemplateFilesRoot = @"..\..\..\ItemDefinitions\ItemDefinitionTemplateFiles\";
  public const string LocalItemTemplatesFolder = @"..\..\..\ItemDefinitions\ItemDefinitionTemplateFolders\";
  
}


