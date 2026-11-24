#!/bin/bash
# Quick deployment reference for TeamBuilder.Orchestrator to Azure AI Foundry
# See FOUNDRY_DEPLOYMENT.md for detailed instructions

set -e

ENVIRONMENT=${1:-dev}
echo "Deploying TeamBuilder.Orchestrator to environment: $ENVIRONMENT"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Verify prerequisites
echo -e "${YELLOW}Step 1: Verifying prerequisites...${NC}"
command -v az >/dev/null 2>&1 || { echo -e "${RED}Azure CLI not found${NC}"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo -e "${RED}Docker not found${NC}"; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo -e "${RED}.NET 10 SDK not found${NC}"; exit 1; }

echo -e "${GREEN}✓ All prerequisites installed${NC}"

# Step 2: Build and test locally
echo -e "${YELLOW}Step 2: Building and testing locally...${NC}"
dotnet build ./TeamBuilder/TeamBuilder.slnx --no-restore
dotnet test ./TeamBuilder.Tests/TeamBuilder.Tests.csproj --no-restore
echo -e "${GREEN}✓ Build and tests passed${NC}"

# Step 3: Get infrastructure info
echo -e "${YELLOW}Step 3: Retrieving infrastructure configuration...${NC}"
SUBSCRIPTION=$(az account show --query id -o tsv)
RESOURCE_GROUP="pokemon-team-builder-$ENVIRONMENT"
AI_PROJECT="pokemon-team-builder-$ENVIRONMENT"
REGISTRY="pokemonregistry$ENVIRONMENT"

echo "Subscription: $SUBSCRIPTION"
echo "Resource Group: $RESOURCE_GROUP"
echo "AI Project: $AI_PROJECT"
echo "Registry: $REGISTRY"

# Step 4: Verify Azure resources exist
echo -e "${YELLOW}Step 4: Verifying Azure resources...${NC}"
if ! az group show --name "$RESOURCE_GROUP" --query id -o tsv >/dev/null 2>&1; then
  echo -e "${RED}Resource group $RESOURCE_GROUP not found${NC}"
  echo "Run infrastructure-release pipeline first to create resources"
  exit 1
fi
echo -e "${GREEN}✓ Resource group exists${NC}"

if ! az ml workspace show --name "$AI_PROJECT" --resource-group "$RESOURCE_GROUP" --query id -o tsv >/dev/null 2>&1; then
  echo -e "${YELLOW}⚠ AI Foundry project not found, creating...${NC}"
  az ml workspace create --name "$AI_PROJECT" --resource-group "$RESOURCE_GROUP" --location eastus
fi
echo -e "${GREEN}✓ AI Foundry project ready${NC}"

# Step 5: Build container image
echo -e "${YELLOW}Step 5: Building container image...${NC}"
IMAGE_TAG="orchestrator:$ENVIRONMENT-latest"
docker build -f ./Dockerfile.orchestrator -t "$IMAGE_TAG" .
echo -e "${GREEN}✓ Container image built: $IMAGE_TAG${NC}"

# Step 6: Push to registry
echo -e "${YELLOW}Step 6: Pushing to Azure Container Registry...${NC}"
REGISTRY_LOGIN_SERVER=$(az acr show --name "$REGISTRY" --query loginServer -o tsv)
docker tag "$IMAGE_TAG" "$REGISTRY_LOGIN_SERVER/$IMAGE_TAG"
az acr login --name "$REGISTRY"
docker push "$REGISTRY_LOGIN_SERVER/$IMAGE_TAG"
echo -e "${GREEN}✓ Container pushed to registry${NC}"

# Step 7: Get deployment info
echo -e "${YELLOW}Step 7: Preparing Foundry deployment...${NC}"
IMAGE_URI="$REGISTRY_LOGIN_SERVER/$IMAGE_TAG"
echo "Image URI: $IMAGE_URI"

# Step 8: Deploy to Foundry
echo -e "${YELLOW}Step 8: Deploying to Azure AI Foundry...${NC}"
DEPLOYMENT_NAME="teambuilder-orchestrator-$ENVIRONMENT"

# Note: This would typically be done via ml client or deployment templates
# For now, output instructions for manual completion
cat << EOF
${YELLOW}
╔════════════════════════════════════════════════════════════════╗
║           Deployment Ready for Azure AI Foundry               ║
╚════════════════════════════════════════════════════════════════╝

Container Image: $IMAGE_URI

Next steps:

1. Manual Foundry Deployment:
   ml client deployment create \\
     --project "$AI_PROJECT" \\
     --environment "$ENVIRONMENT" \\
     --image "$IMAGE_URI" \\
     --name "$DEPLOYMENT_NAME"

2. OR use Azure DevOps pipeline:
   - Queue 'orchestrator-release' pipeline
   - Select environment: $ENVIRONMENT
   - Wait for deployment to complete

3. Verify deployment:
   az ml deployment show \\
     --name "$DEPLOYMENT_NAME" \\
     --workspace-name "$AI_PROJECT" \\
     --resource-group "$RESOURCE_GROUP"

4. Test orchestrator:
   ENDPOINT=\$(az ml deployment show \\
     --name "$DEPLOYMENT_NAME" \\
     --workspace-name "$AI_PROJECT" \\
     --resource-group "$RESOURCE_GROUP" \\
     --query scoring_uri -o tsv)
   
   curl -X POST \$ENDPOINT/api/agent/build-team \\
     -H "Content-Type: application/json" \\
     -d '{"request": "Build a team for gym battles"}'

${GREEN}✓ Orchestrator ready for deployment${NC}
EOF

echo ""
echo -e "${GREEN}Deployment preparation complete!${NC}"
