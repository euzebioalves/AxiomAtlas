# Rollback de produção

Para retornar imagens da aplicação:

```bash
cd /opt/axiom-atlas
./deploy/scripts/rollback.sh 1.0.16
```

O script bloqueia execuções concorrentes, faz backup prévio, troca apenas imagens Web/API/Caddy, valida health e registra o estado. Migrations não são desfeitas automaticamente: elas precisam permanecer retrocompatíveis pelo padrão expand/contract. Quando uma migration não for compatível com a versão anterior, restaure o banco manualmente a partir de backup validado e execute o procedimento de recuperação aprovado.

O workflow `Rollback production` também é manual e exige a aprovação do Environment `production`. Não execute `docker compose down -v` para rollback: isso apaga volumes.
