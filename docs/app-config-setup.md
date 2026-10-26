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

The pipeline identity needs permission to list the AI Foundry account keys (`Microsoft.CognitiveServices/accounts/listKeys/action`). The Key Vault infrastructure step now also grants the signed-in deployment identity **Key Vault Secrets Officer**, scoped only to the environment's vault. It resolves the identity's object ID from the Azure access token without Microsoft Graph queries or additional pipeline variables.

The identity running `teambuilder_connection` must be allowed to create role assignments on that vault, such as through **Role Based Access Control Administrator** at an appropriate scope. Contributor alone cannot grant roles or access secret values. If the role cannot be granted automatically, an administrator must assign it. New RBAC assignments can take several minutes to propagate; rerun infrastructure if the AI Foundry step initially still reports denied secret access.

The AI Foundry step retrieves the account's primary key and synchronizes the `AIAdvisor-ApiKey` vault secret without printing its value. Unchanged keys do not create new secret versions. App Configuration receives only a versionless secret URI under `AIAdvisor:ApiKey`, with no label. Infrastructure already grants the web app's managed identity **Key Vault Secrets User** access for resolving this reference.

When no API key is loaded, the AI client uses `DefaultAzureCredential`. In App Service this is normally the web app's managed identity, not the developer's local Azure login. The AI Foundry step grants that identity **Cognitive Services OpenAI User** on the AI account so it can call chat completions. This role is separate from the pipeline's secret-write permission and the web app's secret-read permission. Allow RBAC propagation after the infrastructure run. A permission error naming the app principal indicates identity-based authentication; check for missing keys, stale labeled overrides, or an app that has not restarted since configuration changed.

All four advisor settings use the null label in each environment's separate store. Existing environment-labeled copies are not deleted automatically. The app loads environment-labeled settings after unlabeled settings, so remove obsolete labeled copies of these four keys when switching to unlabeled defaults; otherwise they still override the defaults. Restart the app after the change.

Rerun the infrastructure pipeline after rotating the AI account's primary key to synchronize the vault secret. The pipeline does not rotate AI account keys itself. Restart the app after synchronization to ensure it loads the updated key. No API key pipeline variable is needed.
