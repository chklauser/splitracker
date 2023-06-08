set windows-shell := ["C:/WINDOWS/system32/wsl.exe", "-d", "Ubuntu", "--exec", "/bin/bash", "-ic"]
export DOCKER_SCAN_SUGGEST := "false"

help:
    @just --list

docker-build-splitracker version='latest':
    dotnet publish --configuration=Release /p:Version={{replace(version,"latest","0.0.0")}}
    
docker-push-splitracker version='latest': (docker-build-splitracker version)
    docker tag splitracker-web:{{version}} registry.hz.klauser.link/splitracker-web:{{version}} 
    docker push registry.hz.klauser.link/splitracker-web:{{version}}

helm-install-splitracker version='latest' env='dev':
    helm upgrade --install --create-namespace --namespace splitracker-{{ env }} \
      {{ if version == "latest" {"--set image.pullPolicy=Always"} else {""} }} \
      --set 'env={{ env }}' \
      --values deploy/splitracker/{{ env }}-values.secret.yaml \
      splitracker ./deploy/splitracker