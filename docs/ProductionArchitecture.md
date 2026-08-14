# Arquitetura de produção

```mermaid
flowchart TB
    Internet --> Caddy["Caddy (80/443, TLS)"]
    Caddy -->|edge| Web["Axiom.Atlas.Web :8080"]
    Web -->|application| Api["Axiom.Atlas.API :8080"]
    Api -->|database| Db[("PostgreSQL :5432")]
    Migrator["migrate (sob demanda)"] --> Db
    Bootstrap["bootstrap-admin (sob demanda)"] --> Db
    Backup["Backup criptografado age + rclone"] --> External["Armazenamento externo"]
```

Somente o Caddy publica `80/tcp` e `443/tcp`. Web, API e PostgreSQL não possuem portas publicadas. As redes `application` e `database` são internas; a API só é alcançada pela Web na rede Docker.

O GitHub constrói, testa e publica imagens imutáveis no GHCR. A VPS apenas faz pull das imagens por versão, aplica migrations com o container temporário e inicia os serviços. Ela nunca compila o projeto.

| Atividade | Responsável |
| --- | --- |
| Release, imagens e ZIPs | GitHub Actions |
| Migração, deploy, rollback e backup | administrador na VPS / workflow manual aprovado |
| TLS e proxy | Caddy na VPS |
| VPS, IP e console de recuperação | Locaweb |
| DNS, SMTP, GitHub Environment e segredos | proprietário do repositório |

Os volumes de PostgreSQL e Data Protection são persistentes. Nunca execute `docker compose down -v`: esse comando apaga os volumes e não faz parte de deploy, rollback ou recuperação.
