class Program {

  #region "Testing stuff"

  static void DisplayCapacities() {
    var capacities = FabricRestApi.GetCapacities();
    foreach (var capacity in capacities) {
      Console.WriteLine(capacity.Sku + " - " + capacity.Id.ToString());
    }
  }

  static void CreateServicePrincipalProfilDisplayName() {
    string ProfileName = "Contoso";
    PowerBiRestApi.CreateSPProfile(ProfileName);
    PowerBiRestApi.DisplaySPProfiles();
  }

  static void UpdateServicePrincipalProfilDisplayName() {
    string ProfileName = "Contoso";
    PowerBiRestApi.UpdateSPProfile(new Guid(AppSettings.ServicePrincipalProfileId), ProfileName);
    PowerBiRestApi.DisplaySPProfiles();
  }

  #endregion

  static void Main(string[] args) {

    //PowerBiRestApi.DisplaySPProfiles();

    //DeploymentManager.Deploy_Pure_PowerBi_Solution("Contoso - Classic PBIE");
    
    DeploymentManager.Deploy_Hybrid_Solution("Contoso");
    
    //DeploymentManager.Deploy_Pure_Fabric_Solution("Contoso-fabric");

    //DeploymentManager.Embed_Report_With_SPP("Contoso");
  }

}
