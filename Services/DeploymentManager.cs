using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;


public class DeploymentManager {

  public static byte[] GetPbixContent(string FileName) {
    return File.ReadAllBytes(AppSettings.LocalPbixFolder + FileName);
  }

  public static void Deploy_Pure_PowerBi_Solution(string TargetWorkspaceName) {

    AppLogger.LogSolution($"Deploy Pure Power BI REST API Solution [{TargetWorkspaceName}]");

    PowerBiRestApi.SetExecutionContextToSpp();

    var workspace = PowerBiRestApi.CreatWorkspace(TargetWorkspaceName);

    byte[] pbixProductSales = DeploymentManager.GetPbixContent("ProductSales.pbix");
    string importName = "Product Sales";

    var import = PowerBiRestApi.ImportPBIX(workspace.Id, pbixProductSales, importName);

    Guid reportId = import.Reports[0].Id;
    Guid datasetId = new Guid(import.Datasets[0].Id);

    AppLogger.LogStep($"Configuring Semantic Model");
    PowerBiRestApi.PatchAnonymousAccessWebCredentials(workspace.Id, datasetId);
    PowerBiRestApi.SetRefreshSchedule(workspace.Id, datasetId.ToString());
    PowerBiRestApi.RefreshDataset(workspace.Id, datasetId);

    OpenWorkspaceInBrowser(workspace.Id.ToString());

    EmbeddedWebPageGenerator.GenerateReportPageAppOwnsData(workspace.Id, reportId);

    AppLogger.LogStep("Solution deployment complete\n");

    AppLogger.PromptUserToContinue();

  }

  public static void Deploy_Hybrid_Solution(string TargetWorkspaceName) {

    AppLogger.LogSolution($"Deploy Hyrbid Fabric Solution [{TargetWorkspaceName}]");

    PowerBiRestApi.SetExecutionContextToSpn();

    Console.WriteLine(">>> AFTER SetExecutionContextToSpn");

    // ---- NEXT CALL INSTRUMENTATION ----

    Console.WriteLine(">>> BEFORE GetWorkspaceByName");
    var existingWorkspace = FabricRestApi.GetWorkspaceByName(TargetWorkspaceName);
    Console.WriteLine(">>> AFTER GetWorkspaceByName");

    Console.WriteLine(">>> BEFORE CreateWorkspace");
    var workspace = existingWorkspace
        ?? FabricRestApi.CreateWorkspace(
            TargetWorkspaceName,
            AppSettings.FabricCapacityId,
            "Hybrid Fabric Workspace");
    Console.WriteLine(">>> AFTER CreateWorkspace");

    AppLogger.LogStep($"Workspace selected: {workspace.DisplayName}");
    AppLogger.LogSubstep($"WorkspaceId: {workspace.Id}");
    AppLogger.LogSubstep($"Workspace URL: https://app.powerbi.com/groups/{workspace.Id}");

    Console.WriteLine(">>> BEFORE AssignWorkspaceToCapacity");
    FabricRestApi.AssignWorkspaceToCapacity(
        workspace.Id,
        new Guid(AppSettings.FabricCapacityId));
    Console.WriteLine(">>> AFTER AssignWorkspaceToCapacity");

    var existingItems = FabricRestApi.ListItems(workspace.Id);
    foreach (var item in existingItems)
    {
        AppLogger.LogSubstep($"Existing item: {item.Type} :: {item.DisplayName}");
    }

    string lakehouseName = "sales";

    var existingLakehouse = FabricRestApi.FindItem(workspace.Id, lakehouseName, ItemType.Lakehouse);
    var lakehouse = existingLakehouse ?? FabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
    AppLogger.LogSubstep(existingLakehouse != null
        ? $"Reusing lakehouse {lakehouse.Id}"
        : $"Created lakehouse {lakehouse.Id}");
  
    // create and run notebook to build bronze layer
    string notebook1Name = "Create Lakehouse Tables";
    var existingNotebook = FabricRestApi.FindItem(workspace.Id, notebook1Name, ItemType.Notebook);
    var notebook1CreateRequest = ItemDefinitionFactory.GetCreateNotebookRequestFromPy(
      workspace.Id,
      lakehouse,
      notebook1Name,
      "CreateLakehouseTables.py");

    var notebook1 = existingNotebook ?? FabricRestApi.CreateItem(workspace.Id, notebook1CreateRequest);
    AppLogger.LogSubstep(existingNotebook != null
        ? $"Reusing notebook {notebook1.Id}"
        : $"Created notebook {notebook1.Id}");

    FabricRestApi.RunNotebook(workspace.Id, notebook1);

    string onelakePath = FabricRestApi.GetOnelakePathForLakehouse(workspace.Id, lakehouse.Id.Value);

    var modelCreateRequest =
      ItemDefinitionFactory.GetCreateItemRequestFromFolder("Product Sales DirectLake Model on Onelake.SemanticModel");

    var semanticModelRedirects = new Dictionary<string, string>() {
      { "{ONELAKE_PATH}", onelakePath }
    };

    modelCreateRequest.Definition =
      ItemDefinitionFactory.UpdateItemDefinitionPart(modelCreateRequest.Definition,
                                                     "definition/expressions.tmdl",
                                                     semanticModelRedirects);

    AppLogger.LogStep($"Creating [{modelCreateRequest.DisplayName}.SemanticModel]");

    var existingModel = FabricRestApi.FindItem(workspace.Id, modelCreateRequest.DisplayName, ItemType.SemanticModel);
    var model = existingModel ?? FabricRestApi.CreateItem(workspace.Id, modelCreateRequest);

    AppLogger.LogSubstep(existingModel != null
        ? $"Reusing semantic model {model.Id.Value}"
        : $"Semantic model created with Id of [{model.Id.Value.ToString()}]");

    var workspaceFabric = FabricRestApi.GetWorkspaceByName(TargetWorkspaceName);

    CreateAndBindSemanticModelConnecton(workspaceFabric, model.Id.Value, lakehouse);

    string reportName = "Product Sales Summary";
    AppLogger.LogStep($"Creating [{reportName}.Report]");

    var createRequestReport =
      ItemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, reportName, "product_sales_summary.json");

    var existingReport = FabricRestApi.FindItem(workspace.Id, reportName, ItemType.Report);
    var report = existingReport ?? FabricRestApi.CreateItem(workspace.Id, createRequestReport);
    AppLogger.LogSubstep(existingReport != null
        ? $"Reusing report {report.Id.Value}"
        : $"Report created with Id of [{report.Id.Value.ToString()}]");

    OpenWorkspaceInBrowser(workspace.Id.ToString());

    PowerBiRestApi.SetExecutionContextToSpp();

    EmbeddedWebPageGenerator.GenerateReportPageAppOwnsData(workspace.Id, report.Id.Value);

    AppLogger.LogStep("Solution deployment complete");

    AppLogger.PromptUserToContinue();

  }

  public static void Deploy_Pure_Fabric_Solution(string TargetWorkspaceName) {

    string lakehouseName = "sales";
    string semanticModelName = "Product Sales DirectLake Model";
    string reportName = "Product Sales Summary";

    AppLogger.LogSolution("Deploy Lakehouse Solution with Notebook");

    AppLogger.LogStep($"Creating new workspace [{TargetWorkspaceName}]");
    var workspace = FabricRestApi.CreateWorkspace(TargetWorkspaceName, AppSettings.FabricCapacityId);
    AppLogger.LogSubstep($"Workspace created with Id of [{workspace.Id.ToString()}]");

    FabricRestApi.UpdateWorkspaceDescription(workspace.Id, "Custom Notebook Solution");

    // create connection to track Web Url for redirects
    //string defaultWebUrl = "https://fabricdevcamp.blob.core.windows.net/sampledata/ProductSales/Dev";
    //var connection = FabricRestApi.CreateAnonymousWebConnection(defaultWebUrl, workspace);

    AppLogger.LogStep($"Creating [{lakehouseName}.Lakehouse]");
    var lakehouse = FabricRestApi.CreateLakehouse(workspace.Id, lakehouseName);
    AppLogger.LogSubstep($"Lakehouse created with Id of [{lakehouse.Id.Value.ToString()}]");

    // create and run notebook to build bronze layer
    string notebook1Name = "Create Lakehouse Tables";
    AppLogger.LogStep($"Creating [{notebook1Name}.Notebook]");
    var notebook1CreateRequest = ItemDefinitionFactory.GetCreateNotebookRequestFromPy(workspace.Id, lakehouse, notebook1Name, "CreateLakehouseTables.py");
    var notebook1 = FabricRestApi.CreateItem(workspace.Id, notebook1CreateRequest);
    AppLogger.LogSubstep($"Notebook created with Id of [{notebook1.Id.Value.ToString()}]");
    AppLogger.LogSubOperationStart($"Running notebook");
    FabricRestApi.RunNotebook(workspace.Id, notebook1);
    AppLogger.LogOperationComplete();

    AppLogger.LogStep("Querying lakehouse properties to get SQL endpoint connection info");
    var sqlEndpoint = FabricRestApi.GetSqlEndpointForLakehouse(workspace.Id, lakehouse.Id.Value);
    AppLogger.LogSubstep($"Server: {sqlEndpoint.ConnectionString}");
    AppLogger.LogSubstep($"Database: " + sqlEndpoint.Id);

    AppLogger.LogStep($"refresh lakehouse");

    FabricRestApi.RefreshLakehouseTableSchema(sqlEndpoint.Id);

    AppLogger.LogStep($"list lakehouse tables");
    var tables = FabricRestApi.ListLakehouseTables(workspace.Id, lakehouse.Id.Value);

    AppLogger.LogStep($"Creating [{semanticModelName}.SemanticModel]");
    var modelCreateRequest =
      ItemDefinitionFactory.GetSemanticDirectLakeModelCreateRequestFromBim(semanticModelName, "sales_model_DirectLake.bim", sqlEndpoint.ConnectionString, sqlEndpoint.Id);

    var model = FabricRestApi.CreateItem(workspace.Id, modelCreateRequest);

    AppLogger.LogSubstep($"Semantic model created with Id of [{model.Id.Value.ToString()}]");

    PowerBiRestApi.SetExecutionContextToSpn();

    CreateAndBindSemanticModelConnecton(workspace, model.Id.Value, lakehouse);

    AppLogger.LogStep($"Creating [{reportName}.Report]");

    var createRequestReport =
      ItemDefinitionFactory.GetReportCreateRequestFromReportJson(model.Id.Value, reportName, "product_sales_summary.json");

    var report = FabricRestApi.CreateItem(workspace.Id, createRequestReport);
    AppLogger.LogSubstep($"Report created with Id of [{report.Id.Value.ToString()}]");

    AppLogger.LogStep("Solution deployment complete");

    AppLogger.PromptUserToContinue();

    OpenWorkspaceInBrowser(workspace.Id);

  }

  public static void Embed_Report_With_SPP(string TargetWorkspaceName) {

    AppLogger.LogSolution($"Embed report using SPP");

    PowerBiRestApi.SetExecutionContextToSpp();

    var workspace = PowerBiRestApi.GetWorkspace(TargetWorkspaceName);

    string reportName = "Product Sales Summary";
    var report = FabricRestApi.GetReportByName(workspace.Id, reportName);

    EmbeddedWebPageGenerator.GenerateReportPageAppOwnsData(workspace.Id, report.Id.Value);

    AppLogger.LogStep("Solution deployment complete");

    AppLogger.PromptUserToContinue();

  }

  public static void View_Workspace_Membership(string TargetWorkspaceName) {

    var workspace = FabricRestApi.GetWorkspaceByName(TargetWorkspaceName);

    FabricRestApi.ViewWorkspaceRoleAssignments(workspace.Id);
    
  }

  public static void Delete_All_Workspace() {

    

  }


    public static void ViewWorkspaceRoleAssignments(Guid workspaceId)
    {
        // Compatibility shim.
        // The Fabric SDK version referenced by this project
        // does not expose workspace role assignment listing.
        // This method is retained for backward compatibility
        // and logging only.

        AppLogger.LogStep("Viewing workspace role assignments");
        AppLogger.LogSubstep("(Workspace role assignment listing not supported by current Fabric SDK)");
    }




    #region Lab Utility Methods

    public static void ViewWorkspaces() {

    var workspaces = FabricRestApi.GetWorkspaces();

    AppLogger.LogStep("Workspaces List");
    foreach (var workspace in workspaces) {
      AppLogger.LogSubstep($"{workspace.DisplayName} ({workspace.Id})");
    }

    Console.WriteLine();

  }

  public static void ViewCapacities() {

    var capacities = FabricRestApi.GetCapacities();

    AppLogger.LogStep("Capacities List");
    foreach (var capacity in capacities) {
      AppLogger.LogSubstep($"[{capacity.Sku}] {capacity.DisplayName} (ID={capacity.Id})");
    }

  }

  private static void OpenWorkspaceInBrowser(Guid WorkspaceId) {
    OpenWorkspaceInBrowser(WorkspaceId.ToString());
  }

  private static void OpenWorkspaceInBrowser(string WorkspaceId) {

    if (!AppLogger.RunInNonInteractiveBatchMode) {
      string url = "https://app.powerbi.com/groups/" + WorkspaceId;
      string chromeBrowserProfileName = "Profile 7";
      var process = new Process();
      process.StartInfo = new ProcessStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
      process.StartInfo.Arguments = url + $" --profile-directory=\"{chromeBrowserProfileName}\" ";
      process.Start();
    }
  }

  public static void CreateAndBindSemanticModelConnecton(Workspace Workspace, Guid SemanticModelId, Item Lakehouse = null) {

    var datasources = PowerBiRestApi.GetDatasourcesForSemanticModelSpn(Workspace.Id, SemanticModelId);


























        foreach (var datasource in datasources) {

      if (datasource.DatasourceType.ToLower() == "web") {
        string url = datasource.ConnectionDetails.Url;

        AppLogger.LogSubstep($"Creating Web connection for semantic model");
        var webConnection = FabricRestApi.CreateAnonymousWebConnection(url, Workspace);

        AppLogger.LogSubstep($"Binding connection to semantic model");
        PowerBiRestApi.BindSemanticModelToConnection(Workspace.Id, SemanticModelId, webConnection.Id);

        AppLogger.LogSubOperationStart($"Refreshing semantic model");
        PowerBiRestApi.RefreshDataset(Workspace.Id, SemanticModelId);
        AppLogger.LogOperationComplete();

      }

      if (datasource.DatasourceType == "AzureDataLakeStorage") {
        AppLogger.LogSubstep($"Creating connection for semantic model");
        string server = datasource.ConnectionDetails.Server;
        string path = datasource.ConnectionDetails.Path;
        var connection = FabricRestApi.CreateAzureStorageConnectionWithServicePrincipal(server, path, Workspace, Lakehouse);
        AppLogger.LogSubstep($"Binding connection to semantic model");
        PowerBiRestApi.BindSemanticModelToConnection(Workspace.Id, SemanticModelId, connection.Id);
        PowerBiRestApi.RefreshDataset(Workspace.Id, SemanticModelId);
      }
    }
  }


  #endregion

 


}
