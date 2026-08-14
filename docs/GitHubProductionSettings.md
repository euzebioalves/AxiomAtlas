# Configurações de produção no GitHub

O proprietário deve configurar manualmente:

- proteção da `main` com PR e checks obrigatórios;
- Environment `production` com revisão/aprovação obrigatória;
- `PROD_HOST`, `PROD_SSH_PORT`, `PROD_USER`, `PROD_SSH_PRIVATE_KEY`, `PROD_KNOWN_HOSTS` e `PROD_DOMAIN` no Environment;
- permissão de escrita em Packages para o workflow de release e acesso de leitura da VPS ao GHCR;
- política de retenção de logs e artefatos;
- confirmação consciente sobre visibilidade pública e licença do repositório.

O deploy usa `ssh` nativo e arquivo `known_hosts` provido pelo segredo. Não configure `StrictHostKeyChecking=no`. A chave privada deve ser exclusiva para a automação e ter acesso mínimo à VPS.

O workflow de release valida build, testes, migration PostgreSQL e imagens antes de calcular/publicar a versão. O commit de incremento, tag e GitHub Release só ocorrem após as validações e publicações de artefatos terem sucesso.
