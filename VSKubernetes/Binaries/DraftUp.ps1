$ErrorActionPreference = "Stop"

echo "Setting Minikube Docker environment variables..."
& $PSScriptRoot\minikube.exe docker-env | Invoke-Expression
echo "Docker environment variables set, draft up..."
& $PSScriptRoot\draft.exe up .
