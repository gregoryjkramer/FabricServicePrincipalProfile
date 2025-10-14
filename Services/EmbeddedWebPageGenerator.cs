using System;
using System.IO;
using System.Diagnostics;
using System.Configuration;

class EmbeddedWebPageGenerator {

  #region "Internal implementation details"

  private static readonly string webPageTemplatesFolder = AppSettings.LocalWebPageTemplatesFolder;
  private static readonly string webPagesFolder = AppSettings.LocalWebPagesFolder;

  static EmbeddedWebPageGenerator() {
    CopyWebSitesFiles();
  }

  public static void CopyWebSitesFiles() {
    Directory.CreateDirectory(webPagesFolder);
    Directory.CreateDirectory(webPagesFolder + "img");

    Directory.CreateDirectory(webPagesFolder + "scripts");

    var filesToCopy = Directory.EnumerateFiles(webPageTemplatesFolder, "*.*", SearchOption.AllDirectories)
      .Where(file => !file.Contains("html"));


    foreach (var sourceFilePath in filesToCopy) {
      var targetFilePath = sourceFilePath.Replace(webPageTemplatesFolder, webPagesFolder);
      byte[] sourceFileContent = File.ReadAllBytes(sourceFilePath);
      File.WriteAllBytes(targetFilePath, sourceFileContent);
    }
   
  }

  static private void LaunchPageInBrowser(string pagePath) {

    string fullPath = Path.GetFullPath(pagePath);
    string chromeBrowserProfileName = "Profile 7";
    var process = new Process();
    process.StartInfo = new ProcessStartInfo(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
    process.StartInfo.Arguments = fullPath + $" --profile-directory=\"{chromeBrowserProfileName}\" ";
    process.Start();
  }

  #endregion

  public static string GetWebPageTemplateFile(string Path) {
    return File.ReadAllText(AppSettings.LocalWebPageTemplatesFolder + Path);
  }

  // demo 02
  public static void GenerateReportPageAppOwnsData(Guid WorkspaceId, Guid ReportId, bool LaunchInBrowser = true) {

    AppLogger.LogStep("Generate Web Page to display report using App-Owns-Data Embedding");

    AppLogger.LogSubstep("Retrieving embed token");
    var embeddingData = PowerBiRestApi.GetReportEmbeddingData(WorkspaceId, ReportId);

    AppLogger.LogSubstep("Generating web page to embed report");

    // parse embedding data into page template
    string htmlSource = GetWebPageTemplateFile("EmbedReport.html");
    string htmlOutput = htmlSource.Replace("@AppName", "Embed Report - App-Owns-Data")
                                  .Replace("@EmbedReportId", embeddingData.reportId)
                                  .Replace("@EmbedUrl", embeddingData.embedUrl)
                                  .Replace("@EmbedToken", embeddingData.accessToken)
                                  .Replace("EmbedTokenType", "models.TokenType.Embed");



    // generate page file on local har drive
    string pagePath = webPagesFolder + "EmbedReport-AppOwnsData.html";
    File.WriteAllText(pagePath, htmlOutput);


    // launch page in browser if requested
    if (LaunchInBrowser) {
      AppLogger.LogSubstep("Opening web page with embedded report in browser");
      LaunchPageInBrowser(pagePath);
    }
  }



  // demo 01
  //public static void GenerateReportPageUserOwnsData(bool LaunchInBrowser = true) {

  //  //// get Power BI embedding data
  //  //var embeddingData = PowerBiApiServiceManager.GetReportEmbeddingDataUserOwnsData();

  //  //// parse embedding data into page template
  //  //string htmlSource = Properties.Resources.EmbedReport_html;
  //  //string htmlOutput = htmlSource.Replace("@AppName", "Demo01: Embed Report - User-Owns-Data")
  //  //                              .Replace("@EmbedReportId", embeddingData.reportId)
  //  //                              .Replace("@EmbedUrl", embeddingData.embedUrl)
  //  //                              .Replace("@EmbedToken", embeddingData.accessToken)
  //  //                              .Replace("EmbedTokenType", "models.TokenType.Aad");


  //  //// generate page file on local har drive
  //  //string pagePath = webPagesFolder + "Demo01-EmbedReport-UserOwnsData.html";
  //  //File.WriteAllText(pagePath, htmlOutput);

  //  //// launch page in browser if requested
  //  //if (LaunchInBrowser) {
  //  //  LaunchPageInBrowser(pagePath);
  //  //}
  //}




}