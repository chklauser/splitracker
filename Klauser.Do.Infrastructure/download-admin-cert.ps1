kubectl get secret -nraven raven-admin-cert -ojsonpath="{.data['keystore\.p12']}" | convertfrom-base64 -AsByteArray | set-content -AsByteStream admin.pfx
kubectl get secret -nraven raven-admin-cert-password-ee75a3df -ojsonpath="{.data.password}" | Convertfrom-base64