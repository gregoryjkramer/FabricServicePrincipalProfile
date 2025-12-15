# Agents

## fabric-hybrid-bootstrap

### Role
Execute the hybrid Fabric + Direct Lake bootstrap flow using the existing
DeploymentManager.Deploy_Hybrid_Solution entry point.

### Scope
- Create or reuse a Fabric workspace on a specified capacity
- Provision a lakehouse, notebook, Direct Lake semantic model, and report
- Optionally generate a static app-owns-data embed page

### Inputs
- All configuration is supplied via AppSettings.cs
- No interactive input is expected at runtime

### Behavioral Rules
- Do not invent configuration values
- Do not change code or templates
- Abort execution if required AppSettings values are missing
- Prefer explicit failure over silent fallback
- Do not expand scope beyond the hybrid bootstrap flow

### Verification Expectations
- Treat successful REST API responses as completion
- Optionally list workspace membership if requested
- Do not assume data refresh or visual readiness

### Stopping Condition
Stop once the deployment flow completes or fails.
Do not attempt retries or remediation.




























































