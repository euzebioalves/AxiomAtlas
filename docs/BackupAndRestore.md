# Backup e restauração

O backup diário usa `pg_dump --format=custom --no-owner --no-acl`, valida com `pg_restore --list`, calcula SHA-256, criptografa com a chave pública `age` e envia para `BACKUP_REMOTE` via `rclone`. O backup só é considerado concluído depois da cópia externa. A chave privada de restauração não fica na VPS.

Também são protegidos, em arquivo criptografado separado: chaves Data Protection de API e Web, `.env`, Compose/Caddy e estado de versão. Dados recriáveis do Caddy não são críticos. O CI executa as rotinas reais de backup em modo local com uma chave `age` efêmera, valida checksum, descriptografa, executa `pg_restore --list`, restaura em banco separado e confirma migration e administrador bootstrap.

Retenção inicial recomendada no destino externo: 7 diários, 4 semanais e 6 mensais. Configure a política no provedor/rclone, pois o script evita apagar a única cópia válida.

```bash
systemctl list-timers axiom-atlas-backup.timer
sudo systemctl start axiom-atlas-backup.service
./deploy/scripts/verify-backup.sh /opt/axiom-atlas/backups/postgres/<arquivo>.age
```

Restauração é destrutiva e exige confirmação explícita. Ela cria um backup prévio, para Web/API, verifica checksum, descriptografa, valida conteúdo, restaura e testa health:

```bash
BACKUP_AGE_IDENTITY_FILE=/caminho/seguro/identity.txt \
  ./deploy/scripts/restore-postgres.sh --confirm /caminho/backup.dump.age
```

Teste a restauração primeiro em PostgreSQL temporário, isolado da produção. Nunca apague o backup usado na restauração.
