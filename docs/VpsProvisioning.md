# Provisionamento da VPS Locaweb

1. Contrate VPS Ubuntu Server 24.04 LTS e anote IP/console Locaweb.
2. Gere uma chave SSH exclusiva; conecte inicialmente como `root` em duas sessões.
3. Crie `atlasadmin`, inclua em `sudo`, copie a chave pública e teste login/sudo na segunda sessão.
4. Só depois do teste desabilite login remoto de root e autenticação por senha em `sshd_config`; mantenha o console Locaweb como recuperação.
5. Copie o repositório/pacote para `/opt/axiom-atlas`, mantenha posse de `atlasadmin` e rode como root:

```bash
/opt/axiom-atlas/deploy/scripts/provision-vps.sh
```

O script instala Docker oficial/Compose v2, ferramentas de backup, Fail2ban, atualizações de segurança, UTC, swap de emergência de 2 GB, diretórios, UFW e timer de backup. Não bloqueia SSH além de limitar tentativas. O UFW deve permitir apenas `22`, `80` e `443`; portas Docker publicadas podem contornar regras do UFW, portanto somente Caddy publica portas.

6. Execute `docker login ghcr.io` como `atlasadmin` usando token de leitura de packages.
7. Copie `deploy/.env.example` para `/opt/axiom-atlas/.env`, preencha segredos e execute `chmod 600`.
8. Configure DNS A/AAAA para o IP antes de iniciar Caddy. Configure SMTP, rclone e a chave pública `age`; guarde a chave privada fora da VPS.
9. Execute migration, bootstrap e deploy: `deploy.sh <versão>`. Após o primeiro bootstrap, remova a senha do administrador do `.env`.

O monitoramento inicial deve observar URL pública e health da Web, CPU >70% por 15 min, RAM >80%, swap persistente, disco 70%/80%, reinícios de container, validade TLS e ausência de backup válido por 26 horas. O script `status.sh` reúne os indicadores locais.
