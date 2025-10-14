using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Fabric.Api.Core.Models;

public class PlatformFileConfig {
  public string version { get; set; }
  public string logicalId { get; set; }
}

public class PlatformFileMetadata {
  public string type { get; set; }
  public string displayName { get; set; }
}

public class FabricPlatformFile {
  [JsonPropertyName("$schema")]
  public string schema { get; set; }
  public PlatformFileMetadata metadata { get; set; }
  public PlatformFileConfig config { get; set; }
}


public class ItemDefinitionFactory {

  public static ItemDefinition UpdateItemDefinitionPart(ItemDefinition ItemDefinition, string PartPath, Dictionary<string, string> SearchReplaceText) {
    var itemPart = ItemDefinition.Parts.Where(part => part.Path == PartPath).FirstOrDefault();
    if (itemPart != null) {
      ItemDefinition.Parts.Remove(itemPart);
      itemPart.Payload = SearchAndReplaceInPayload(itemPart.Payload, SearchReplaceText);
      ItemDefinition.Parts.Add(itemPart);
    }
    return ItemDefinition;
  }

  public static string SearchAndReplaceInPayload(string Payload, Dictionary<string, string> SearchReplaceText) {
    byte[] PayloadBytes = Convert.FromBase64String(Payload);
    string PayloadContent = Encoding.UTF8.GetString(PayloadBytes, 0, PayloadBytes.Length);
    foreach (var entry in SearchReplaceText.Keys) {
      PayloadContent = PayloadContent.Replace(entry, SearchReplaceText[entry]);
    }
    return Convert.ToBase64String(Encoding.UTF8.GetBytes(PayloadContent));
  }

  public static string GetTemplateFile(string Path) {
    return File.ReadAllText(AppSettings.LocalTemplateFilesRoot + Path);
  }

  private static string GetPartPath(string ItemFolderPath, string FilePath) {
    int ItemFolderPathOffset = ItemFolderPath.Length + 1;
    return FilePath.Substring(ItemFolderPathOffset).Replace("\\", "/");
  }

  public static ItemDefinitionPart CreateInlineBase64Part(string Path, string Payload) {
    string base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(Payload));
    return new ItemDefinitionPart(Path, base64Payload, PayloadType.InlineBase64);
  }

  public static CreateItemRequest GetSemanticModelCreateRequestFromBim(string DisplayName, string BimFile) {

    string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
    string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}");

    var createRequest = new CreateItemRequest(DisplayName, ItemType.SemanticModel);

    createRequest.Definition =
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
      });

    return createRequest;
  }

  public static UpdateItemDefinitionRequest GetSemanticModelUpdateRequestFromBim(string DisplayName, string BimFile) {

    string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
    string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}");

    return new UpdateItemDefinitionRequest(
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
      }));
  }

  public static CreateItemRequest GetSemanticDirectLakeModelCreateRequestFromBim(string DisplayName, string BimFile, string SqlEndpointServer, string SqlEndpointDatabase) {

    string part1FileContent = GetTemplateFile(@"SemanticModels\definition.pbism");
    string part2FileContent = GetTemplateFile($@"SemanticModels\{BimFile}")
                                               .Replace("{SQL_ENDPOINT_SERVER}", SqlEndpointServer)
                                               .Replace("{SQL_ENDPOINT_DATABASE}", SqlEndpointDatabase);

    var createRequest = new CreateItemRequest(DisplayName, ItemType.SemanticModel);

    createRequest.Definition =
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbism", part1FileContent),
        CreateInlineBase64Part("model.bim", part2FileContent)
      });

    return createRequest;
  }

  public static CreateItemRequest GetReportCreateRequestFromReportJson(Guid SemanticModelId, string DisplayName, string ReportJson) {

    string part1FileContent = GetTemplateFile(@"Reports\definition.pbir").Replace("{SEMANTIC_MODEL_ID}", SemanticModelId.ToString());
    string part2FileContent = GetTemplateFile($@"Reports\{ReportJson}");
    string part3FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BaseThemes\CY24SU02.json");

    var createRequest = new CreateItemRequest(DisplayName, ItemType.Report);

    createRequest.Definition =
          new ItemDefinition(new List<ItemDefinitionPart>() {
            CreateInlineBase64Part("definition.pbir", part1FileContent),
            CreateInlineBase64Part("report.json", part2FileContent),
            CreateInlineBase64Part("StaticResources/SharedResources/BaseThemes/CY24SU02.json", part3FileContent),
          });

    return createRequest;

  }

  public static UpdateItemDefinitionRequest GetUpdateRequestFromReportJson(Guid SemanticModelId, string DisplayName, string ReportJson) {

    string part1FileContent = GetTemplateFile(@"Reports\definition.pbir").Replace("{SEMANTIC_MODEL_ID}", SemanticModelId.ToString());
    string part2FileContent = GetTemplateFile($@"Reports\{ReportJson}");
    string part3FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BaseThemes\CY24SU02.json");
    string part4FileContent = GetTemplateFile(@"Reports\StaticResources\SharedResources\BuiltInThemes\NewExecutive.json");

    return new UpdateItemDefinitionRequest(
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("definition.pbir", part1FileContent),
        CreateInlineBase64Part("report.json", part2FileContent),
        CreateInlineBase64Part("StaticResources/SharedResources/BaseThemes/CY24SU02.json", part3FileContent),
        CreateInlineBase64Part("StaticResources/SharedResources/BuiltInThemes/NewExecutive.json", part4FileContent)
      }));
  }

  public static CreateItemRequest GetCreateNotebookRequestFromPy(Guid WorkspaceId, Item Lakehouse, string DisplayName, string PyFile) {

    var pyContent = GetTemplateFile($@"Notebooks\{PyFile}").Replace("{WORKSPACE_ID}", WorkspaceId.ToString())
                                                           .Replace("{LAKEHOUSE_ID}", Lakehouse.Id.ToString())
                                                           .Replace("{LAKEHOUSE_NAME}", Lakehouse.DisplayName);

    var createRequest = new CreateItemRequest(DisplayName, ItemType.Notebook);

    createRequest.Definition =
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("notebook-content.py", pyContent)
      });

    return createRequest;

  }

  public static CreateItemRequest GetCreateNotebookRequestFromIpynb(Guid WorkspaceId, Item Lakehouse, string DisplayName, string IpynbFile) {

    var ipynbContent = GetTemplateFile($@"Notebooks\{IpynbFile}").Replace("{WORKSPACE_ID}", WorkspaceId.ToString())
                                                                 .Replace("{LAKEHOUSE_ID}", Lakehouse.Id.ToString())
                                                                 .Replace("{LAKEHOUSE_NAME}", Lakehouse.DisplayName);

    var createRequest = new CreateItemRequest(DisplayName, ItemType.Notebook);

    createRequest.Definition =
      new ItemDefinition(new List<ItemDefinitionPart>() {
        CreateInlineBase64Part("notebook-content.ipynb", ipynbContent)
      });

    createRequest.Definition.Format = "ipynb";

    return createRequest;

  }




  public static CreateItemRequest GetCreateItemRequestFromFolder(string ItemFolder) {

    string ItemFolderPath = AppSettings.LocalItemTemplatesFolder + ItemFolder;

    string metadataFilePath = ItemFolderPath + @"\.platform";
    string metadataFileContent = File.ReadAllText(metadataFilePath);
    PlatformFileMetadata item = JsonSerializer.Deserialize<FabricPlatformFile>(metadataFileContent).metadata;

    CreateItemRequest itemCreateRequest = new CreateItemRequest(item.displayName, item.type);

    var parts = new List<ItemDefinitionPart>();

    List<string> ItemDefinitionFiles = Directory.GetFiles(ItemFolderPath, "*", SearchOption.AllDirectories).ToList<string>();

    foreach (string ItemDefinitionFile in ItemDefinitionFiles) {

      string fileContentBase64 = Convert.ToBase64String(File.ReadAllBytes(ItemDefinitionFile));

      parts.Add(new ItemDefinitionPart(GetPartPath(ItemFolderPath, ItemDefinitionFile), fileContentBase64, "InlineBase64"));

    }

    itemCreateRequest.Definition = new ItemDefinition(parts);

    return itemCreateRequest;
  }

  public static ItemDefinition UpdateReportDefinitionWithSemanticModelId(ItemDefinition ItemDefinition, Guid TargetModelId) {
    var partDefinition = ItemDefinition.Parts.Where(part => part.Path == "definition.pbir").First();
    ItemDefinition.Parts.Remove(partDefinition);
    string reportDefinitionPartTemplate = ItemDefinitionFactory.GetTemplateFile(@"Reports\definition.pbir");
    string reportDefinitionPartContent = reportDefinitionPartTemplate.Replace("{SEMANTIC_MODEL_ID}", TargetModelId.ToString());
    var reportDefinitionPart = ItemDefinitionFactory.CreateInlineBase64Part("definition.pbir", reportDefinitionPartContent);
    ItemDefinition.Parts.Add(reportDefinitionPart);
    return ItemDefinition;
  }


}