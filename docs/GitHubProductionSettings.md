# Configurações de produção no GitHub

O proprietário deve configurar manualmente a proteção da `main` com:

- **Require a pull request before merging**;
- **Require status checks to pass** e selecionar exatamente `Validate application and production stack`;
- **Require conversation resolution**;
- **Block force pushes**;
- **Block branch deletion**;
- não permitir bypass para `github-actions[bot]`.

Crie também uma regra/ruleset para `v*` que impeça alteração, force update e exclusão indevida das tags de release.

O Environment `production` deve exigir aprovação manual e conter exclusivamente `PROD_HOST`, `PROD_SSH_PORT`, `PROD_USER`, `PROD_SSH_PRIVATE_KEY`, `PROD_KNOWN_HOSTS` e `PROD_DOMAIN`. Mantenha política de retenção de logs/artefatos, confirmação sobre a visibilidade/licença do repositório e acesso de leitura da VPS ao GHCR.

O deploy usa `ssh` nativo e arquivo `known_hosts` provido pelo segredo. Não configure `StrictHostKeyChecking=no`. A chave privada deve ser exclusiva para a automação e ter acesso mínimo à VPS.

O CI usa somente `contents: read`. O job de validação do release também usa somente `contents: read`; apenas o job de publicação recebe `contents: write` e `packages: write`, necessários para criar a tag/release e publicar no GHCR. Deploy e rollback são manuais, usam `production`, `contents: read` (e `packages: read` no deploy), SSH com `known_hosts` e nunca desabilitam a verificação de host.

O workflow de release valida build, testes, migration PostgreSQL, plano Bake, imagens e assets antes de publicar. Não há commit nem push automático na `main`: `VersionPrefix` é apenas baseline/fallback, a versão final é calculada pelas tags estáveis e injetada nos binários por MSBuild. A tag aponta para o SHA incorporado à `main`; imagens, assets e GitHub Release usam o mesmo SHA. Reexecuções do mesmo SHA reutilizam a versão, tag, imagens e rascunho existentes. Colisões de tag ou imagem com outra revisão falham, e tags de versão não devem ser alteradas.
