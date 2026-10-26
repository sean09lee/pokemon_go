# App Configuration Setup

The infrastructure pipeline configures the advisor settings in App Configuration. Secret values are stored in Key Vault and exposed through Key Vault references.

The pipeline adds all four settings with no label:

- `AIAdvisor:ApiKey`: Key Vault reference.
- `AIAdvisor:DeploymentName`: deployed model name.
- `AIAdvisor:Endpoint`: AI Foundry endpoint URL.
- `AIAdvisor:SystemPrompt`: the default advisor instructions below.

The default system prompt is:

> You are an expert Pokémon team-building advisor named Professor Oak. Help users build balanced, competitive teams by analyzing type coverage, synergy, and common threats. Keep responses concise and actionable.

## API Key Permissions

The pipeline identity needs permission to list the AI Foundry account keys (`Microsoft.CognitiveServices/accounts/listKeys/action`) and the **Key Vault Secrets Officer** role on the environment's vault. A management-plane Contributor role alone does not grant access to Key Vault secret values. Assign the vault role to the service principal used by `teambuilder_connection`, then allow time for RBAC propagation.

The AI Foundry step retrieves the account's primary key and synchronizes the `AIAdvisor-ApiKey` vault secret without printing its value. Unchanged keys do not create new secret versions. App Configuration receives only a versionless secret URI under `AIAdvisor:ApiKey`, with no label. Infrastructure already grants the web app's managed identity **Key Vault Secrets User** access for resolving this reference.

All four advisor settings use the null label in each environment's separate store. Existing environment-labeled copies are not deleted automatically. The app loads environment-labeled settings after unlabeled settings, so remove obsolete labeled copies of these four keys when switching to unlabeled defaults; otherwise they still override the defaults. Restart the app after the change.

Rerun the infrastructure pipeline after rotating the AI account's primary key to synchronize the vault secret. The pipeline does not rotate AI account keys itself. Restart the app after synchronization to ensure it loads the updated key. No API key pipeline variable is needed.
