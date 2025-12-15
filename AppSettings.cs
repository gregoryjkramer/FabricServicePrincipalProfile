public class AppSettings {

  public const string FabricRestApiBaseUrl = "https://api.fabric.microsoft.com/v1";
  public const string PowerBiRestApiBaseUrl = "https://api.powerbi.com";
  public const string OneLakeBaseUrl = "https://onelake.dfs.fabric.microsoft.com";

  // TODO: configure capacity Id for Fabric-enabled capacity 
  public const string FabricCapacityId = "124A057D-D0C8-4F66-BD7B-BFE1E68FF3ED";

  // TODO: configure authentication mode
  public static AppAuthenticationMode AuthenticationMode = AppAuthenticationMode.ServicePrincipalAuth;

  // TODO: configure Entra Id application for service principal auth
  public const string ServicePrincipalAuthTenantId = "f325857e-a2a1-4724-802a-37e74d5c60cc";
  public const string ServicePrincipalAuthClientId = "06d61021-a495-48a7-9e03-a60ccd109027";
  public const string ServicePrincipalAuthClientSecret = "REDACTED";
  public const string ServicePrincipalObjectId = "7b1d1dd6-6dbf-4f8b-8c36-dbee0129274f";

  public const string ServicePrincipalProfileId = "00000000-0000-0000-0000-000000000000";

  // TODO: configure object id of Entra Id user account of user running demo
  public const string AdminUserId = "11d3da23-b57d-46ab-85ca-f768a57b2490"; // Entra ID for user

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


