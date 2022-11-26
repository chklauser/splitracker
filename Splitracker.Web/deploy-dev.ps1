Param(
    [Parameter(Mandatory=$true)]
    [string]
    $version
)
$ErrorActionPreference = "Stop"

dotnet publish -p Configuration=Release
docker tag splitracker-web:1.0.0 registry.digitalocean.com/klauser/splitracker:$version
docker push registry.digitalocean.com/klauser/splitracker:$version

push-location ../Splitracker.Deploy
try {
    pulumi config set --stack dev version $version
    pulumi up --stack dev -f
} finally {
    pop-location
}
