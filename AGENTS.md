# Agents

## fabric-hybrid-bootstrap

### Role
Execute the hybrid Fabric + Direct Lake bootstrap flow using the existing
DeploymentManager.Deploy_Hybrid_Solution entry point.

### Scope
- Create workspace (timestamped if name collision)
- Provision a lakehouse, notebook, Direct Lake semantic model, and report
- Generate static embed page

### Inputs
- All configuration is supplied via AppSettings.cs
- Terminal prompt may occur unless batch mode is enabled

### Behavioral Rules
- ### Behavioral Rules
- Do not invent configuration values
- Do not change code or templates
- Abort execution if *mandatory* AppSettings values are missing
  (tenant ID, client ID, client secret, capacity ID)
- Service Principal Profile (SPP) execution is preferred when a
  ServicePrincipalProfileId is provided; otherwise execution
  falls back to SPN context
- Prefer explicit logging over silent failure
- Do not expand scope beyond the hybrid bootstrap flow


### Verification Expectations
- Treat successful REST API responses as completion
- Optionally list workspace membership if requested
- Do not assume data refresh or visual readiness

### Stopping Condition
Stop once the deployment flow completes or fails.
Do not attempt retries or remediation.




























































