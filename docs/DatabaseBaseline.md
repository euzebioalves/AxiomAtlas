# Baseline do banco de dados

## Motivo da mudança

A cadeia histórica continha `20260321013144_InitialIdentity` com `Up()` vazio, enquanto migrations posteriores assumiam tabelas Identity já existentes. Logo, um PostgreSQL vazio não conseguia formar um schema confiável. A baseline `InitialProductionBaseline` foi gerada a partir do `AppDbContext`, do modelo atual e do snapshot, criando Identity, tabelas funcionais, índices, constraints e relacionamentos em uma única migration.

Não existe banco de produção anterior. A migration é aplicada somente pelo container `migrate`:

```bash
docker compose --env-file /opt/axiom-atlas/.env -f /opt/axiom-atlas/deploy/compose.prod.yml run --rm migrate
```

## Bancos locais antigos

Esta baseline não deve ser aplicada diretamente em um banco com a cadeia antiga. Antes de qualquer ação, faça backup. Para recriar um ambiente local descartável, remova **somente** o banco local explicitamente escolhido e execute o migrador. A ação é destrutiva e não é automatizada por este PR.

Para preservar um banco existente, exporte dados e valide o schema em uma cópia. A adoção segura exige confirmar todas as tabelas e índices da baseline e registrar manualmente `InitialProductionBaseline` na tabela `__EFMigrationsHistory`; não há script automático porque uma divergência de schema poderia mascarar perda de dados. Esse procedimento deve ser feito por DBA responsável, após backup testado.
