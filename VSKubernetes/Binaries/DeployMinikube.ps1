$ErrorActionPreference = "Stop"

function Exec
{
    param (
        [Parameter(Position=0, Mandatory=1)]
        [scriptblock]$Command
    )
    & $Command
    if ($LastExitCode -ne 0) {
        throw "Execution failed:`n$Command"
    }
}

function Retry
{
    param (
        [Parameter(Position=0, Mandatory=1)]
        [scriptblock]$Command,
        [Parameter(Position=1)]
        [int]$SleepInterval = 1,
        [Parameter(Position=2)]
        [int]$Retries = 60
     )

    for ($i=1; $i -le $Retries; $i++) {
        try {
            Exec $Command
            break
        } catch {
            Start-Sleep $SleepInterval
            if ($i -eq $Retries) {
                throw
            }
        }
    }
}

function GetVMSwitchName()
{
    $vmSwitches = (Get-VMSwitch -SwitchType External)
    if (!$vmSwitches) {
        throw "Please create an external Hyper-V VM switch"
    }
    return $vmSwitches[0].Name
}

if (Exec { .\minikube.exe status } | select-string "Running") {
    Exec { .\minikube.exe delete }
}

Exec { .\minikube.exe start --vm-driver=hyperv --hyperv-virtual-switch="$(GetVMSwitchName)" --cpus=4 --memory=4096 --disk-size=20g }
Exec { .\helm.exe init }
Exec { .\minikube.exe addons enable ingress }
Exec { .\minikube addons enable registry }

$minikubeIp = Exec { $(.\minikube ip) }

# Wait for tiller to become ready
Retry { .\helm.exe list 2>&1 | Out-Null }

$ENV:DRAFT_BASE_DOMAIN="${minikubeIp}.xip.io"
Exec { .\draft.exe init --yes }
