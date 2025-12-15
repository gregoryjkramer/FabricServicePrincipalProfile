using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api;
using Microsoft.Rest;
using System.Net.Http.Headers;
using System;
using Microsoft.PowerBI.Api.Models.Credentials;
using System.Text;
using Microsoft.Fabric.Api.Core.Models;

// data required for embedding a report
public class ReportEmbeddingData {
  public string reportId;
  public string reportName;
  public string embedUrl;
  public string accessToken;
}

public class PowerBiRestApi {

  #region "Mgmt of access tokens and PowerBIClient for SPN and SPP"

  private static PowerBIClient pbiClient;

  private static PowerBIClient pbiClientSpn;
  private static PowerBIClient pbiClientSpp;

  static PowerBiRestApi() {

    var accessTokenResult = EntraIdTokenManager.GetAccessTokenResult(PowerBiPermissionScopes.Default);
    var tokenCredentials = new TokenCredentials(accessTokenResult.AccessToken, "Bearer");
    string urlPowerBiServiceApiRoot = AppSettings.PowerBiRestApiBaseUrl;

    pbiClientSpn = new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials);

    // Only create SPP client if a valid Service Principal Profile ID is configured
    if (Guid.TryParse(AppSettings.ServicePrincipalProfileId, out Guid servicePrincipalProfileId) && 
        servicePrincipalProfileId != Guid.Empty) {
      Guid servicePrinciaplProfileId = new Guid(AppSettings.ServicePrincipalProfileId);
      pbiClientSpp = new PowerBIClient(new Uri(urlPowerBiServiceApiRoot), tokenCredentials, servicePrinciaplProfileId);
      pbiClient = pbiClientSpp;
    }
    else {
      // If no SPP is configured, use SPN client by default
      pbiClientSpp = pbiClientSpn;
      pbiClient = pbiClientSpn;
    }

  }

  public static void SetExecutionContextToSpn() {
    AppLogger.LogSectionHeader("Switching context to execute as Service Principal (SPN)");
    pbiClient = pbiClientSpn;
  }

  public static void SetExecutionContextToSpp() {
    AppLogger.LogSectionHeader("Switching context to execute as Service Principal Profile (SPP)");
    pbiClient = pbiClientSpp;
  }

  #endregion

  #region "Service Principal Profile CRUD"

  public static void DisplaySPProfiles() {
    var profiles = pbiClientSpn.Profiles.GetProfiles().Value;
    if (profiles.Count == 0) {
      AppLogger.LogStep("There are no service principal profiles");
    }
    else {
      AppLogger.LogStep($"List of service principal profiles");
      foreach (var profile in profiles) {
        AppLogger.LogSubstep(profile.Id.ToString() + ": " + profile.DisplayName);
      }
    }
  }

  public static void CreateSPProfile(string Name) {
    AppLogger.LogStep($"Creating service principal profile: {Name}");
    CreateOrUpdateProfileRequest createRequest = new CreateOrUpdateProfileRequest {
      DisplayName = Name
    };

    var profile = pbiClientSpn.Profiles.CreateProfile(createRequest);
    AppLogger.LogSubstep($"Profile created with id {profile.Id.ToString()}");
  }

  public static void UpdateSPProfile(Guid ProfileId, string Name) {
    AppLogger.LogStep($"Updating service principal profile to: {Name}");
    CreateOrUpdateProfileRequest updateRequest = new CreateOrUpdateProfileRequest {
      DisplayName = Name
    };

    var profile = pbiClientSpn.Profiles.UpdateProfile(ProfileId, updateRequest);
    AppLogger.LogSubstep($"Profile with id {profile.Id.ToString()} has been updated");
  }

  #endregion

  #region "Basic Power BI REST API Helper Methods"

  public static void DeleteAllWorkspaces() {

    SetExecutionContextToSpp();
    var workspaces = pbiClient.Groups.GetGroups().Value;
    if (workspaces.Count == 0) {
      AppLogger.LogStep("There are no workspaces for SPP");
    }
    else {
      AppLogger.LogStep("Deleting Workspaces owned by SPP");
      foreach (var workspace in workspaces) {
        AppLogger.LogSubstep("  deleting " + workspace.Name + " - [" + workspace.Id + "]");
        DeleteWorkspace(workspace.Id);
      }
    }
    Console.WriteLine();

    SetExecutionContextToSpn();
    workspaces = pbiClient.Groups.GetGroups().Value;
    if (workspaces.Count == 0) {
      AppLogger.LogStep("There are no workspaces for SPN");
    }
    else {
      AppLogger.LogStep("Deleting Workspaces owned by SPN");
      foreach (var workspace in workspaces) {
        AppLogger.LogSubstep("  deleting " + workspace.Name + " - [" + workspace.Id + "]");
        DeleteWorkspace(workspace.Id);
      }
    }
    Console.WriteLine();


  }

  public static void DisplayWorkspaces() {

    SetExecutionContextToSpp();
    var workspaces = pbiClient.Groups.GetGroups().Value;
    if (workspaces.Count == 0) {
      AppLogger.LogStep("There are no workspaces for SPP");
    }
    else {
      AppLogger.LogStep("Workspace owned by SPP");
      foreach (var workspace in workspaces) {
        AppLogger.LogSubstep("  " + workspace.Name + " - [" + workspace.Id + "]");
      }
    }
    Console.WriteLine();

    SetExecutionContextToSpn();
    workspaces = pbiClient.Groups.GetGroups().Value;
    if (workspaces.Count == 0) {
      AppLogger.LogStep("There are no workspaces for SPN");
    }
    else {
      AppLogger.LogStep("Workspace owned by SPN");
      foreach (var workspace in workspaces) {
        AppLogger.LogSubstep("  " + workspace.Name + " - [" + workspace.Id + "]");
      }
    }
    Console.WriteLine();



  }

  public static Group GetWorkspace(string Name) {
    // build search filter with workspace name
    string filter = "name eq '" + Name + "'";
    var workspaces = pbiClient.Groups.GetGroups(filter: filter).Value;
    if (workspaces.Count == 0) {
      return null;
    }
    else {
      return workspaces.First();
    }
  }

  public static void DeleteWorkspace(Guid WorkspaceId) {
    pbiClient.Groups.DeleteGroup(WorkspaceId);
  }

  public static Dataset GetDataset(Guid WorkspaceId, string DatasetName) {
    var datasets = pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
    foreach (var dataset in datasets) {
      if (dataset.Name.Equals(DatasetName)) {
        return dataset;
      }
    }
    return null;
  }

  public static void GetDatasourcesForWorkspace(string WorkspaceName) {

    Console.WriteLine();
    Console.WriteLine("Generating JSON files for each datasource in workspace");

    Group workspace = GetWorkspace(WorkspaceName);
    var datasets = pbiClient.Datasets.GetDatasetsInGroup(workspace.Id).Value;
    foreach (var dataset in datasets) {
      var datasources = pbiClient.Datasets.GetDatasourcesInGroup(workspace.Id, dataset.Id).Value;      
    }

    var reports = pbiClient.Reports.GetReportsInGroup(workspace.Id).Value;
    foreach (var report in reports) {
      if (report.ReportType != "PowerBIReport") {
        var datasources = pbiClient.Reports.GetDatasourcesInGroup(workspace.Id, report.Id).Value;
      }
    }

    var dataflows = pbiClient.Dataflows.GetDataflows(workspace.Id).Value;
    foreach (var dataflow in dataflows) {
      var datasources = pbiClient.Dataflows.GetDataflowDataSources(workspace.Id, dataflow.ObjectId).Value;
    }

  }

  #endregion

  #region "Methods for Creating Workspaces and Importing PBIX Files"

  public static Group CreatWorkspace(string Name) {

    AppLogger.LogStep($"Creating new workspace named [{Name}]");

    // delete workspace with same name if it already exists
    Group workspace = GetWorkspace(Name);
    if (workspace != null) {
      AppLogger.LogSubstep("Deleting existing workspace with the same name");
      pbiClient.Groups.DeleteGroup(workspace.Id);
      workspace = null;
    }

    // create new workspace
    GroupCreationRequest request = new GroupCreationRequest(Name);
    try {
      workspace = pbiClient.Groups.CreateGroup(request);
    }
    catch (Microsoft.Rest.HttpOperationException ex) {
      AppLogger.LogStep($"Error creating workspace: {ex.Message}");
      AppLogger.LogStep($"Status Code: {ex.Response.StatusCode}");
      AppLogger.LogStep($"Response Content: {ex.Response.Content}");
      throw;
    }

    AppLogger.LogSubstep($"New workspace created with Id of [{workspace.Id}]");

    AssignWorkspaceToCapacity(workspace);

    AddAdminUserAsWorkspaceAdmin(workspace);

    return workspace;
    
  }

  public static void AssignWorkspaceToCapacity(Group Workspace) {
    AppLogger.LogSubstep($"Assign workspace to capacity [{AppSettings.FabricCapacityId}]");
    pbiClient.Groups.AssignToCapacity(Workspace.Id, new AssignToCapacityRequest {
      CapacityId = new Guid(AppSettings.FabricCapacityId),
    });
  }

  public static void AddAdminUserAsWorkspaceAdmin(Group Workspace) {
    AppLogger.LogSubstep($"Adding Admin User to workspace as admin");
    pbiClient.Groups.AddGroupUser(Workspace.Id, new Microsoft.PowerBI.Api.Models.GroupUser {
      Identifier = AppSettings.AdminUserId,
      PrincipalType = Microsoft.PowerBI.Api.Models.PrincipalType.User,
      EmailAddress = "greg@tenaciousdata.com    ",
      GroupUserAccessRight = "Admin"
    });
  }

  public static void AddServicePrincipalAsWorkspaceAdmin(Microsoft.Fabric.Api.Core.Models.Workspace workspace) {
    AppLogger.LogSubstep($"Adding SPN to workspace as admin");
    pbiClient.Groups.AddGroupUser(workspace.Id,
      new Microsoft.PowerBI.Api.Models.GroupUser {
        Identifier = AppSettings.ServicePrincipalObjectId,
        PrincipalType = Microsoft.PowerBI.Api.Models.PrincipalType.App,
        GroupUserAccessRight = "Admin"
      });

  }

  public static Import ImportPBIX(Guid WorkspaceId, byte[] PbixContent, string ImportName) {
    AppLogger.LogOperationStart($"Importing PBIX file for {ImportName}.");

    MemoryStream stream = new MemoryStream(PbixContent);
    var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId, stream, ImportName, ImportConflictHandlerMode.CreateOrOverwrite);

    do {
      Thread.Sleep(2000);
      import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id);
      AppLogger.LogOperationInProgress();
    }
    while (import.ImportState.Equals("Publishing"));
    
    AppLogger.LogOperationComplete();
    AppLogger.LogSubstep($"PBIX file imported");

    Guid reportId = import.Reports[0].Id;
    Guid datasetId = new Guid(import.Datasets[0].Id);
    AppLogger.LogSubstep($"Imported report Id: {reportId.ToString()}");
    AppLogger.LogSubstep($"Imported dataset Id: {datasetId.ToString()}");

    return import;
  }

  public static void RefreshDataset(Guid WorkspaceId, string DatasetId) {
    pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId);
  }

  public static void RefreshDataset(Guid WorkspaceId, Guid DatasetId) {

    AppLogger.LogSubOperationStart("Refreshing dataset");

    var refreshRequest = new DatasetRefreshRequest {
      NotifyOption = NotifyOption.NoNotification,
      Type = DatasetRefreshType.Automatic
    };

    var responseStartFresh = pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId.ToString(), refreshRequest);

    var responseStatusCheck = pbiClient.Datasets.GetRefreshExecutionDetailsInGroup(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));

    while (responseStatusCheck.Status == "Unknown") {
      AppLogger.LogOperationInProgress();
      Thread.Sleep(2000);
      AppLogger.LogOperationInProgress();
      responseStatusCheck = pbiClient.Datasets.GetRefreshExecutionDetailsInGroup(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));
    }

    AppLogger.LogOperationComplete();

    if (responseStatusCheck.Status == "Failed") {
      AppLogger.LogSubOperationStart("Refresh failed. Trying again");
      Thread.Sleep(15000);
      responseStartFresh = pbiClient.Datasets.RefreshDatasetInGroup(WorkspaceId, DatasetId.ToString(), refreshRequest);

      responseStatusCheck = pbiClient.Datasets.GetRefreshExecutionDetailsInGroup(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));

      while (responseStatusCheck.Status == "Unknown") {
        Thread.Sleep(10000);
        responseStatusCheck = pbiClient.Datasets.GetRefreshExecutionDetailsInGroup(WorkspaceId, DatasetId, new Guid(responseStartFresh.XMsRequestId));
      }

    }

  }

  public static void SetRefreshSchedule(Guid WorkspaceId, string DatasetId) {

    AppLogger.LogSubstep("Setting refresh schedule");

    var schedule = new RefreshSchedule {
      Enabled = true,
      Days = new List<Days?> {
          Days.Monday,
          Days.Tuesday,
          Days.Wednesday,
          Days.Thursday,
          Days.Friday
        },
      Times = new List<string> {
          "02:00",
          "11:30"
        },
      LocalTimeZoneId = "UTC",
      NotifyOption = AppSettings.AuthenticationMode != AppAuthenticationMode.ServicePrincipalAuth ? ScheduleNotifyOption.MailOnFailure : ScheduleNotifyOption.NoNotification
    };

    pbiClient.Datasets.UpdateRefreshSchedule(WorkspaceId, DatasetId, schedule);

  }

  public static Import ImportRDL(Guid WorkspaceId, string RdlFileContent, string ImportName) {
    Console.WriteLine("Importing RDL for " + ImportName);

    string rdlImportName = ImportName + ".rdl";

    byte[] byteArray = Encoding.ASCII.GetBytes(RdlFileContent);
    MemoryStream RdlFileContentStream = new MemoryStream(byteArray);

    var import = pbiClient.Imports.PostImportWithFileInGroup(WorkspaceId,
                                                             RdlFileContentStream,
                                                             rdlImportName,
                                                             ImportConflictHandlerMode.Abort);

    // poll to determine when import operation has complete
    do { import = pbiClient.Imports.GetImportInGroup(WorkspaceId, import.Id); }
    while (import.ImportState.Equals("Publishing"));

    return import;

  }

  #endregion

  #region "Connections and Datasources"

  public static void PatchAnonymousAccessWebCredentials(Guid WorkspaceId, Guid DatasetId) {

    AppLogger.LogSubstep("Patching anonymous web credetials");

    // get datasources for dataset
    var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId.ToString()).Value;

    foreach (var datasource in datasources) {

      // check to ensure datasource use Web connector
      if (datasource.DatasourceType.ToLower() == "web") {

        // get DatasourceId and GatewayId
        var datasourceId = datasource.DatasourceId;
        var gatewayId = datasource.GatewayId;

        // Initialize UpdateDatasourceRequest object with AnonymousCredentials
        UpdateDatasourceRequest req = new UpdateDatasourceRequest {
          CredentialDetails = new Microsoft.PowerBI.Api.Models.CredentialDetails(
            new Microsoft.PowerBI.Api.Models.Credentials.AnonymousCredentials(),
            Microsoft.PowerBI.Api.Models.PrivacyLevel.Organizational,
            Microsoft.PowerBI.Api.Models.EncryptedConnection.NotEncrypted)
        };

        // Update datasource credentials through Gateways - UpdateDatasource
        pbiClient.Gateways.UpdateDatasource((Guid)gatewayId, (Guid)datasourceId, req);

      }
    }
  }

  public static IList<Datasource> GetDatasourcesForDataset(string WorkspaceId, string DatasetId) {
    return pbiClient.Datasets.GetDatasourcesInGroup(new Guid(WorkspaceId), DatasetId).Value;
  }

  public static IList<Report> GetReportsInWorkspace(Guid WorkspaceId) {
    return pbiClient.Reports.GetReportsInGroup(WorkspaceId).Value;
  }

  public static IList<Dataset> GetDatasetsInWorkspace(Guid WorkspaceId) {
    return pbiClient.Datasets.GetDatasetsInGroup(WorkspaceId).Value;
  }

  public static void ViewDatasources(Guid WorkspaceId, Guid DatasetId) {

    // get datasources for dataset
    var datasources = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId.ToString()).Value;

    foreach (var datasource in datasources) {

      Console.WriteLine(" - Connection Name: " + datasource.Name);
      Console.WriteLine("   > DatasourceType: " + datasource.DatasourceType);
      Console.WriteLine("   > DatasourceId: " + datasource.DatasourceId);
      Console.WriteLine("   > GatewayId: " + datasource.GatewayId);
      Console.WriteLine("   > Path: " + datasource.ConnectionDetails.Path);
      Console.WriteLine("   > Server: " + datasource.ConnectionDetails.Server);
      Console.WriteLine("   > Database: " + datasource.ConnectionDetails.Database);
      Console.WriteLine("   > Url: " + datasource.ConnectionDetails.Url);
      Console.WriteLine("   > Domain: " + datasource.ConnectionDetails.Domain);
      Console.WriteLine("   > EmailAddress: " + datasource.ConnectionDetails.EmailAddress);
      Console.WriteLine("   > Kind: " + datasource.ConnectionDetails.Kind);
      Console.WriteLine("   > LoginServer: " + datasource.ConnectionDetails.LoginServer);
      Console.WriteLine("   > ClassInfo: " + datasource.ConnectionDetails.ClassInfo);
      Console.WriteLine();

    }
  }

  public static IList<Datasource> GetDatasourcesForSemanticModel(Guid WorkspaceId, Guid DatasetId) {
    return pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId.ToString()).Value;
  }

  public static IList<Datasource> GetDatasourcesForSemanticModelSpn(Guid WorkspaceId, Guid DatasetId) {
    return pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId.ToString()).Value;
  }

  public static string GetWebDatasourceUrl(Guid WorkspaceId, Guid DatasetId) {

    var datasource = pbiClient.Datasets.GetDatasourcesInGroup(WorkspaceId, DatasetId.ToString()).Value.First();
    if (datasource.DatasourceType.Equals("Web")) {
      return datasource.ConnectionDetails.Url;
    }
    else {
      throw new ApplicationException("Error - expecting Web connection");
    }
  }

  public static void BindReportToSemanticModel(Guid WorkspaceId, Guid SemanticModelId, Guid ReportId) {
    RebindReportRequest bindRequest = new RebindReportRequest(SemanticModelId.ToString());
    pbiClient.Reports.RebindReportInGroup(WorkspaceId, ReportId, bindRequest);
  }

  public static void BindSemanticModelToConnection(Guid WorkspaceId, Guid SemanticModelId, Guid ConnectionId) {

    BindToGatewayRequest bindRequest = new BindToGatewayRequest {
      DatasourceObjectIds = new List<Guid?>()
    };

    bindRequest.DatasourceObjectIds.Add(ConnectionId);

    pbiClient.Datasets.BindToGatewayInGroup(WorkspaceId, SemanticModelId.ToString(), bindRequest);

  }

  #endregion

  #region "Power BI Embed Token Generation"

  public static ReportEmbeddingData GetReportEmbeddingData(Guid WorkspaceId, Guid ReportId) {


    var report = pbiClient.Reports.GetReportInGroup(WorkspaceId, ReportId);
    var embedUrl = "https://app.powerbi.com/reportEmbed";
    var reportName = report.Name;
    var datasetId = report.DatasetId;

    var workspaceRequests = new List<GenerateTokenRequestV2TargetWorkspace>();
    workspaceRequests.Add(new GenerateTokenRequestV2TargetWorkspace(WorkspaceId));

    var datasetRequests = new List<GenerateTokenRequestV2Dataset>();
    datasetRequests.Add(new GenerateTokenRequestV2Dataset(datasetId.ToString(), XmlaPermissions.ReadOnly));

    var reportRequests = new List<GenerateTokenRequestV2Report>();
    reportRequests.Add(new GenerateTokenRequestV2Report(ReportId, allowEdit: true));


    GenerateTokenRequestV2 tokenRequest =
      new GenerateTokenRequestV2 {
        Datasets = datasetRequests,
        Reports = reportRequests,
        TargetWorkspaces = workspaceRequests
      };

    // call to Power BI Service API and pass GenerateTokenRequest object to generate embed token
    var EmbedTokenResult = pbiClient.EmbedToken.GenerateToken(tokenRequest);

    return new ReportEmbeddingData {
      reportId = ReportId.ToString(),
      reportName = reportName,
      embedUrl = embedUrl,
      accessToken = EmbedTokenResult.Token
    };

  }

  #endregion

}

public static class PowerBiPermissionScopes {
     public static readonly string[] Default = { "https://analysis.windows.net/powerbi/api/.default" };
   }

