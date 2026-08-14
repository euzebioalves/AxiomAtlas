# Implantação em produção

Pré-requisitos: VPS provisionada, DNS apontado, `.env` seguro, login da VPS no GHCR, backup externo configurado e release disponível.

```bash
cd /opt/axiom-atlas
./deploy/scripts/deploy.sh 1.0.17
```

O deploy valida versão, ambiente, espaço livre e lock; executa backup local+externo criptografado; faz pull das imagens da versão; executa `migrate`; atualiza somente Web/API/Caddy; aguarda `/health/live` e `/health/ready`; e registra versão atual/anterior. Se migration ou backup falhar, o deploy falha antes de atualizar a aplicação.

O workflow `Deploy production` é exclusivamente manual (`workflow_dispatch`) e usa o Environment `production`, que deve exigir aprovação. Ele valida tag e imagens no GHCR, fixa `known_hosts` e executa o mesmo script na VPS. Não há deploy automático após merge ou release.

Para validar localmente a topologia, use uma cópia local de `.env` com valores de teste:

```bash
docker compose --env-file .env -f deploy/compose.prod.yml -f deploy/compose.local.yml build
docker compose --env-file .env -f deploy/compose.prod.yml -f deploy/compose.local.yml run --rm migrate
docker compose --env-file .env -f deploy/compose.prod.yml -f deploy/compose.local.yml up -d
./deploy/scripts/smoke-local.sh
```
