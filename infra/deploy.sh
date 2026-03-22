#!/usr/bin/env bash
set -euo pipefail

RESOURCE_GROUP="${RESOURCE_GROUP:-flightprep-rg}"
LOCATION="${LOCATION:-westeurope}"
DB_PASSWORD="${DB_PASSWORD:?DB_PASSWORD env var required}"

az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$(dirname "$0")/main.bicep" \
  --parameters dbAdminPassword="$DB_PASSWORD" \
  --output table

echo "Deployment complete."
