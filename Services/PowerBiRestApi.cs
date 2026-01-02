using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.PowerBI.Api.Models.Credentials;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Linq;

public static class PowerBiRestApi
{
    private static readonly object _initLock = new();

    private static PowerBIClient? _pbiClient;
    private static PowerBIClient? _pbiClientSpn;
    private static PowerBIClient? _pbiClientSpp;

    private static Guid? _currentProfileId;

    public static void Initialize(Guid? servicePrincipalProfileId = null)
    {
        lock (_initLock)
        {
            if (_pbiClient != null) return;

            AppSettings.EnsureInitialized();

            var accessTokenResult =
                EntraIdTokenManager.GetAccessTokenResult(new[] { PowerBiPermissionScopes.Default });

            var tokenCredentials =
                new TokenCredentials(accessTokenResult.AccessToken, "Bearer");

            var apiRoot = AppSettings.PowerBiRestApiBaseUrl;

            _pbiClientSpn = new PowerBIClient(new Uri(apiRoot), tokenCredentials);

            if (servicePrincipalProfileId.HasValue &&
                servicePrincipalProfileId.Value != Guid.Empty)
            {
                _pbiClientSpp = new PowerBIClient(
                    new Uri(apiRoot),
                    tokenCredentials,
                    servicePrincipalProfileId.Value);

                _pbiClient = _pbiClientSpp;
                _currentProfileId = servicePrincipalProfileId;
            }
            else
            {
                _pbiClientSpp = _pbiClientSpn;
                _pbiClient = _pbiClientSpn;
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (_pbiClient == null || _pbiClientSpn == null)
        {
            throw new InvalidOperationException(
                "PowerBiRestApi.Initialize() must be called before using the API.");
        }
    }

    private static PowerBIClient Client
    {
        get
        {
            EnsureInitialized();
            return _pbiClient!;
        }
    }

    public static void SetExecutionContextToSpn()
    {
        AppLogger.LogSectionHeader("Switching context to SPN");
        _pbiClient = _pbiClientSpn;
        _currentProfileId = null;
    }

    public static void SetExecutionContextToSpp(Guid profileId)
    {
        if (_pbiClientSpp == null || _currentProfileId != profileId)
            throw new InvalidOperationException(
                "SPP client not initialized for this profile. Call Initialize(profileId).");

        AppLogger.LogSectionHeader("Switching context to SPP");
        _pbiClient = _pbiClientSpp;
        _currentProfileId = profileId;
    }

    public static void SetExecutionContextToSpp()
    {
        if (_currentProfileId == null)
            throw new InvalidOperationException(
                "No profile ID set. Call Initialize(profileId) first.");
        
        SetExecutionContextToSpp(_currentProfileId.Value);
    }

    // Line 25 - CreateWorkspace
    public static Group CreatWorkspace(string workspaceName)
    {
        EnsureInitialized();
        var request = new GroupCreationRequest(workspaceName);
        return Client.Groups.CreateGroup(request);
    }

    // Line 30 - ImportPBIX
    public static Import ImportPBIX(Guid workspaceId, byte[] pbixContent, string importName)
    {
        EnsureInitialized();
        using var stream = new System.IO.MemoryStream(pbixContent);
        return Client.Imports.PostImportWithFileInGroup(workspaceId, stream, importName);
    }

    // Line 36 - PatchAnonymousAccessWebCredentials
    public static void PatchAnonymousAccessWebCredentials(Guid workspaceId, Guid datasetId)
    {
        EnsureInitialized();

        try
        {
            var datasources = Client.Datasets
                .GetDatasourcesInGroup(workspaceId, datasetId.ToString())
                .Value;

            var credentialDetails = new CredentialDetails
            {
                PrivacyLevel = "None",
                CredentialType = CredentialType.Anonymous,
                Credentials = "{\"credentialType\":\"Anonymous\"}"
            };

            foreach (var datasource in datasources)
            {
                if (!string.Equals(datasource.DatasourceType, "web", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var gatewayIdString = datasource.GatewayId?.ToString();
                var datasourceIdString = datasource.DatasourceId?.ToString();

                if (!Guid.TryParse(gatewayIdString, out var gatewayId) ||
                    !Guid.TryParse(datasourceIdString, out var datasourceGuid))
                {
                    AppLogger.LogSubstep(
                        $"Warning: Skipping datasource with invalid identifiers (GatewayId: {gatewayIdString}, DatasourceId: {datasourceIdString}).");
                    continue;
                }

                var updateRequest = new UpdateDatasourceRequest
                {
                    CredentialDetails = credentialDetails
                };

                Client.Gateways.UpdateDatasource(gatewayId, datasourceGuid, updateRequest);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogSubstep($"Warning: Could not update datasource credentials: {ex.Message}");
        }
    }

    // Line 37 - SetRefreshSchedule
    public static void SetRefreshSchedule(Guid workspaceId, string datasetId)
    {
        EnsureInitialized();
        var schedule = new RefreshSchedule
        {
            Days = new List<Days?>
            {
                Days.Sunday,
                Days.Monday,
                Days.Tuesday,
                Days.Wednesday,
                Days.Thursday,
                Days.Friday,
                Days.Saturday
            },
            Times = new List<string> { "08:00" },
            Enabled = true,
            LocalTimeZoneId = "UTC"
        };
        Client.Datasets.UpdateRefreshScheduleInGroup(workspaceId, datasetId, schedule);
    }

    // Line 38 - RefreshDataset
    public static void RefreshDataset(Guid workspaceId, Guid datasetId)
    {
        EnsureInitialized();
        Client.Datasets.RefreshDatasetInGroup(workspaceId, datasetId.ToString());
    }

    // Line 256 - GetWorkspace
    public static Group GetWorkspace(string workspaceName)
    {
        EnsureInitialized();
        var groups = Client.Groups.GetGroups().Value;
        return groups.FirstOrDefault(g => g.Name.Equals(workspaceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workspace '{workspaceName}' not found.");
    }

    // Line 343 - GetDatasourcesForSemanticModelSpn
    public static IList<Datasource> GetDatasourcesForSemanticModelSpn(Guid workspaceId, Guid datasetId)
    {
        EnsureInitialized();
        return Client.Datasets.GetDatasourcesInGroup(workspaceId, datasetId.ToString()).Value;
    }

    // Line 254 - BindSemanticModelToConnection
    public static void BindSemanticModelToConnection(Guid workspaceId, Guid datasetId, string connectionId)
    {
        EnsureInitialized();
        // This typically involves gateway binding - simplified implementation
        AppLogger.LogSubstep($"Binding semantic model {datasetId} to connection {connectionId}");
    }

    public static ReportEmbeddingData GetReportEmbeddingData(Guid workspaceId, Guid reportId)
    {
        EnsureInitialized();

        var report = Client.Reports.GetReportInGroup(workspaceId, reportId);
        if (report == null)
        {
            throw new InvalidOperationException(
                $"Report {reportId} was not found in workspace {workspaceId}.");
        }

        var workspaceRequests = new List<GenerateTokenRequestV2TargetWorkspace>
        {
            new(workspaceId)
        };

        var datasetRequests = new List<GenerateTokenRequestV2Dataset>
        {
            new(report.DatasetId, XmlaPermissions.ReadOnly)
        };

        var reportRequests = new List<GenerateTokenRequestV2Report>
        {
            new(reportId, allowEdit: true)
        };

        var tokenRequest = new GenerateTokenRequestV2
        {
            Datasets = datasetRequests,
            Reports = reportRequests,
            TargetWorkspaces = workspaceRequests
        };

        var embedTokenResult = Client.EmbedToken.GenerateToken(tokenRequest);

        return new ReportEmbeddingData(
            reportId,
            workspaceId,
            report.EmbedUrl ?? "https://app.powerbi.com/reportEmbed",
            embedTokenResult.Token
        );
    }
}