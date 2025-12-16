using Azure.Core;
using Microsoft.Fabric;
using Microsoft.Fabric.Api;
using Microsoft.Fabric.Api.Core.Models;
using Microsoft.Fabric.Api.Eventhouse.Models;
using Microsoft.Fabric.Api.Report.Models;
using Microsoft.Fabric.Api.SemanticModel.Models;
using Microsoft.Fabric.Api.Utils;
using System.Net.Http.Headers;
using System.Text;

using FabricAdmin = Microsoft.Fabric.Api.Admin.Models;
using LakehouseModels = Microsoft.Fabric.Api.Lakehouse.Models;
using NotebookModels = Microsoft.Fabric.Api.Notebook.Models;
using WarehouseModels = Microsoft.Fabric.Api.Warehouse.Models;

public static class FabricRestApi
{
    // =========================
    // Initialization state
    // =========================
    private static bool _initialized;
    private static string? _accessToken;
    private static FabricClient? _fabricApiClient;

    /// <summary>
    /// Must be called exactly once after AppSettings + Key Vault are initialized.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized)
            return;

        var accessTokenResult = EntraIdTokenManager.GetAccessTokenResult();
        _accessToken = accessTokenResult.AccessToken;

        _fabricApiClient = new FabricClient(
            _accessToken,
            new Uri(AppSettings.FabricRestApiBaseUrl));

        _initialized = true;
    }

    /// <summary>
    /// Guarded accessor used everywhere instead of touching fields directly.
    /// </summary>
    private static FabricClient FabricClient
    {
        get
        {
            if (!_initialized || _fabricApiClient is null)
                throw new InvalidOperationException(
                    "FabricRestApi.Initialize() must be called before use.");

            return _fabricApiClient;
        }
    }

    // =========================
    // Workspaces
    // =========================

    public static List<Workspace> GetWorkspaces()
    {
        var all = FabricClient.Core.Workspaces.ListWorkspaces().ToList();
        return all.Where(w => w.Type == WorkspaceType.Workspace).ToList();
    }

    public static List<Capacity> GetCapacities()
    {
        return FabricClient.Core.Capacities.ListCapacities().ToList();
    }

    public static Capacity GetCapacity(Guid capacityId)
    {
        return GetCapacities().First(c => c.Id == capacityId);
    }

    public static string GetOnelakePathForLakehouse(Guid workspaceId, Guid lakehouseId)
    {
        var lakehouse = FabricClient
            .Lakehouse
            .Items
            .GetLakehouse(workspaceId, lakehouseId)
            .Value;

        return lakehouse.Properties.OneLakeTablesPath.Replace("Tables", "");
    }

    public static Workspace UpdateWorkspaceDescription(Guid workspaceId, string description)
    {
        var updateRequest = new UpdateWorkspaceRequest
        {
            Description = description
        };

        return FabricClient
            .Core
            .Workspaces
            .UpdateWorkspace(workspaceId, updateRequest)
            .Value;
    }

    public static LakehouseModels.SqlEndpointProperties GetSqlEndpointForLakehouse(
    Guid workspaceId,
    Guid lakehouseId)
    {
        // Get lakehouse item
        var item = FabricClient
            .Core
            .Items
            .GetItem(workspaceId, lakehouseId)
            .Value;

        if (item.Type != ItemType.Lakehouse)
            throw new InvalidOperationException("Item is not a Lakehouse.");

        // Get item definition (this is where lakehouse metadata lives)
        var definitionResponse = FabricClient
            .Core
            .Items
            .GetItemDefinition(workspaceId, lakehouseId, format: "json")
            .Value;


        // TODO: Lakehouse definition parsing requires SDK-specific handling.
        // The current Fabric SDK returns ItemDefinition, not raw JSON.
        // This will be implemented after refactor stabilization.
        throw new NotSupportedException(
            "Lakehouse SQL endpoint inspection not supported in current refactor phase.");
    }



    public static Report? GetReportByName(Guid workspaceId, string reportName)
    {
        return FabricClient
            .Report
            .Items
            .ListReports(workspaceId)
            .FirstOrDefault(r =>
                r.DisplayName != null &&
                r.DisplayName.Equals(reportName, StringComparison.OrdinalIgnoreCase));
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

    public static Workspace? GetWorkspaceByName(Guid workspaceId)
    {
        return FabricClient
            .Core
            .Workspaces
            .GetWorkspace(workspaceId);
    }

    public static Workspace? GetWorkspaceByName(string workspaceName)
    {
        return FabricClient
            .Core
            .Workspaces
            .ListWorkspaces()
            .FirstOrDefault(w =>
                w.DisplayName != null &&
                w.DisplayName.Equals(workspaceName, StringComparison.OrdinalIgnoreCase));
    }

    public static Connection CreateAnonymousWebConnection(
    string url,
    Workspace workspace)
    {
        string displayName = $"Workspace[{workspace.Id}]-Web";

        var creationMethodParams = new List<ConnectionDetailsParameter>
    {
        new ConnectionDetailsTextParameter("url", url)
    };

        var createConnectionDetails = new CreateConnectionDetails(
            "Web",
            "Web",
            creationMethodParams);

        var creds = new AnonymousCredentials();

        var createCredentialDetails = new CreateCredentialDetails(creds)
        {
            SkipTestConnection = false
        };

        var request = new CreateCloudConnectionRequest(
            displayName,
            createConnectionDetails,
            createCredentialDetails);

        return CreateConnection(request);
    }


    public static WorkspaceInfo GetWorkspaceInfo(Guid workspaceId)
    {
        return FabricClient.Core.Workspaces.GetWorkspace(workspaceId);
    }

    public static Workspace CreateWorkspace(
        string workspaceName,
        string? capacityId = null,
        string? description = null)
    {
        capacityId ??= AppSettings.FabricCapacityId;

        var existing = GetWorkspaceByName(workspaceName);
        if (existing != null)
        {
            workspaceName = $"{workspaceName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        var req = new CreateWorkspaceRequest(workspaceName)
        {
            Description = description
        };

        var workspaceResponse = FabricClient.Core.Workspaces.CreateWorkspace(req);
        var workspace = workspaceResponse.Value;

        if (AppSettings.AuthenticationMode == AppAuthenticationMode.ServicePrincipalAuth &&
            AppSettings.AdminUserId != "00000000-0000-0000-0000-000000000000")
        {
            AddUserAsWorkspaceMember(
                workspace.Id,
                new Guid(AppSettings.AdminUserId),
                WorkspaceRole.Admin);
        }
        else if (AppSettings.ServicePrincipalObjectId != "00000000-0000-0000-0000-000000000000")
        {
            AddServicePrincipalAsWorkspaceMember(
                workspace.Id,
                new Guid(AppSettings.ServicePrincipalObjectId),
                WorkspaceRole.Admin);
        }




























        if (!string.IsNullOrWhiteSpace(capacityId))
        {
            AssignWorkspaceToCapacity(workspace.Id, new Guid(capacityId));
        }

        return workspace;
    }

    public static void AssignWorkspaceToCapacity(Guid workspaceId, Guid capacityId)
    {
        var req = new AssignWorkspaceToCapacityRequest(capacityId);

        if (AppSettings.AuthenticationMode == AppAuthenticationMode.ServicePrincipalAuth)
        {
            var userToken = EntraIdTokenManager.GetFabricAccessTokenForUser();
            var userClient = new FabricClient(
                userToken,
                new Uri(AppSettings.FabricRestApiBaseUrl));

            var capacities = userClient.Core.Capacities.ListCapacities();
            if (capacities.Any(c => c.Id == capacityId && c.Sku == "FT1"))
            {
                userClient.Core.Workspaces.AssignToCapacity(workspaceId, req);
                return;
            }
        }

        FabricClient.Core.Workspaces.AssignToCapacity(workspaceId, req);
    }

    // =========================
    // Connections
    // =========================

    public static List<Connection> GetConnections()
    {
        return FabricClient.Core.Connections.ListConnections().ToList();
    }

    public static Connection? GetConnectionByName(string name)
    {
        return GetConnections()
            .FirstOrDefault(c => c.DisplayName == name);
    }

    public static Connection CreateConnection(CreateConnectionRequest req)
    {
        var existing = GetConnectionByName(req.DisplayName);
        if (existing != null)
            return existing;

        var conn = FabricClient.Core.Connections.CreateConnection(req).Value;

        if (AppSettings.AuthenticationMode == AppAuthenticationMode.ServicePrincipalAuth &&
            AppSettings.AdminUserId != "00000000-0000-0000-0000-000000000000")
        {
            AddConnectionRoleAssignmentForUser(
                conn.Id,
                new Guid(AppSettings.AdminUserId),
                ConnectionRole.Owner);
        }
        else if (AppSettings.ServicePrincipalObjectId != "00000000-0000-0000-0000-000000000000")
        {
            AddConnectionRoleAssignmentForServicePrincipal(
                conn.Id,
                new Guid(AppSettings.ServicePrincipalObjectId),
                ConnectionRole.Owner);
        }

        return conn;
    }

    public static void AddConnectionRoleAssignmentForUser(
        Guid connectionId,
        Guid userId,
        ConnectionRole role)
    {
        var p = new Principal(userId, PrincipalType.User);
        var r = new AddConnectionRoleAssignmentRequest(p, role);
        FabricClient.Core.Connections.AddConnectionRoleAssignment(connectionId, r);
    }

    public static void AddConnectionRoleAssignmentForServicePrincipal(
        Guid connectionId,
        Guid spId,
        ConnectionRole role)
    {
        var p = new Principal(spId, PrincipalType.ServicePrincipal);
        var r = new AddConnectionRoleAssignmentRequest(p, role);
        FabricClient.Core.Connections.AddConnectionRoleAssignment(connectionId, r);
    }

    // =========================
    // Items / Lakehouse / Notebooks
    // =========================

    public static List<Item> ListItems(Guid workspaceId)
    {
        return FabricClient.Core.Items.ListItems(workspaceId).ToList();
    }

    public static Item? FindItem(Guid workspaceId, string displayName, ItemType type)
    {
        return ListItems(workspaceId).FirstOrDefault(i =>
            i.DisplayName != null &&
            i.DisplayName.Equals(displayName, StringComparison.OrdinalIgnoreCase) &&
            i.Type == type);
    }

    public static Item CreateItem(Guid workspaceId, CreateItemRequest req)
    {
        return FabricClient.Core.Items
            .CreateItemAsync(workspaceId, req)
            .Result.Value;
    }

    public static Item CreateLakehouse(
        Guid workspaceId,
        string name,
        bool enableSchemas = false)
    {
        var req = new CreateItemRequest(name, ItemType.Lakehouse);
        if (enableSchemas)
        {
            req.CreationPayload = new List<KeyValuePair<string, object>>
{
    new("enableSchemas", true)
};
        }

        return CreateItem(workspaceId, req);
    }

    public static void RefreshLakehouseTableSchema(string SqlEndpointId)
    {
        string restUri =
            $"{AppSettings.PowerBiRestApiBaseUrl}/v1.0/myorg/lhdatamarts/{SqlEndpointId}";

        HttpContent body =
            new StringContent("{ \"commands\":[{ \"$type\":\"MetadataRefreshCommand\"}]}");

        body.Headers.ContentType =
            new MediaTypeWithQualityHeaderValue("application/json");

        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _accessToken);

        HttpResponseMessage response = client.PostAsync(restUri, body).Result;
    }


    public static void RefreshLakehouseTableSchema(Guid sqlEndpointId)
    {
        // Forward to the original implementation
        RefreshLakehouseTableSchema(sqlEndpointId.ToString());
    }

    public static List<LakehouseModels.Table> ListLakehouseTables(
    Guid workspaceId,
    Guid lakehouseId)
    {
        // TEMPORARY compatibility shim.
        // The old SDK exposed Lakehouse.Tables directly.
        // The new SDK does not. We restore correctness later.
        return new List<LakehouseModels.Table>();
    }

    public static Connection CreateAzureStorageConnectionWithServicePrincipal(
    string server,
    string path,
    Workspace workspace,
    Item lakehouse)
    {
        // Build display name consistent with legacy behavior
        string displayName =
            $"Workspace[{workspace.Id}]-Lakehouse[{lakehouse.DisplayName}]-ADLS";

        string connectionType = "AzureDataLakeStorage";
        string creationMethod = "AzureDataLakeStorage";

        var creationMethodParams = new List<ConnectionDetailsParameter>
    {
        new ConnectionDetailsTextParameter("server", server),
        new ConnectionDetailsTextParameter("path", path)
    };

        var createConnectionDetails =
            new CreateConnectionDetails(
                connectionType,
                creationMethod,
                creationMethodParams);

        var creds = new ServicePrincipalCredentials(
            new Guid(AppSettings.ServicePrincipalAuthTenantId),
            new Guid(AppSettings.ServicePrincipalAuthClientId),
            AppSettings.ServicePrincipalAuthClientSecret);

        var credentialDetails =
            new CreateCredentialDetails(creds)
            {
                SingleSignOnType = SingleSignOnType.None,
                ConnectionEncryption = ConnectionEncryption.NotEncrypted,
                SkipTestConnection = false
            };

        var request =
            new CreateCloudConnectionRequest(
                displayName,
                createConnectionDetails,
                credentialDetails);

        return CreateConnection(request);
    }








    public static NotebookModels.Notebook? GetNotebookByName(
        Guid workspaceId,
        string name)
    {
        return FabricClient.Notebook.Items
            .ListNotebooks(workspaceId)
            .FirstOrDefault(n => n.DisplayName == name);
    }

    public static void RunNotebook(
        Guid workspaceId,
        Item notebook,
        RunOnDemandItemJobRequest? req = null)
    {
        var response = FabricClient.Core.JobScheduler
            .RunOnDemandItemJob(
                workspaceId,
                notebook.Id!.Value,
                "RunNotebook",
                req);

        if (response.Status != 202)
            throw new InvalidOperationException("Notebook start failed.");

        Guid jobId = response.GetTriggeredJobId();

        while (true)
        {
            Thread.Sleep(5000);
            var job = FabricClient.Core.JobScheduler
                .GetItemJobInstance(
                    workspaceId,
                    notebook.Id.Value,
                    jobId)
                .Value;

            if (job.Status == Status.Succeeded)
                return;

            if (job.Status == Status.Failed)
                throw new InvalidOperationException(job.FailureReason?.Message);
        }
    }

    private static void AddUserAsWorkspaceMember(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role)
    {
        var principal = new Principal(userId, PrincipalType.User);
        var request = new AddWorkspaceRoleAssignmentRequest(principal, role);
        FabricClient.Core.Workspaces.AddWorkspaceRoleAssignment(workspaceId, request);
    }

    private static void AddServicePrincipalAsWorkspaceMember(
        Guid workspaceId,
        Guid objectId,
        WorkspaceRole role)
    {
        var principal = new Principal(objectId, PrincipalType.ServicePrincipal);
        var request = new AddWorkspaceRoleAssignmentRequest(principal, role);
        FabricClient.Core.Workspaces.AddWorkspaceRoleAssignment(workspaceId, request);
    }
}
