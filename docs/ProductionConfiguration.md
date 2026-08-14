# Configuração de produção

Copie `deploy/.env.example` para `/opt/axiom-atlas/.env`, preencha valores reais e aplique `chmod 600 /opt/axiom-atlas/.env`. O `.env` é ignorado pelo Git e não deve ser copiado para tickets, logs ou releases.

Gere senhas e o segredo JWT com fonte criptograficamente segura. `JwtSettings__SecretKey` deve ter ao menos 32 caracteres. `PublicUrls__WebBaseUrl` deve ser uma URL HTTPS pública; `ApiSettings__BaseUrl` da Web é automaticamente `http://api:8080/` no Compose e nunca deve ser uma URL pública.

As aplicações falham cedo em produção quando faltam connection string, JWT, Data Protection, URL pública, SMTP ou cookie. Placeholders `CHANGE_ME`, URLs públicas em HTTP e `localhost` na URL pública são rejeitados.

Após o bootstrap inicial, remova `BOOTSTRAP_ADMIN_PASSWORD` do `.env`. Use o painel administrativo para criar os demais usuários. O processo de bootstrap é idempotente e não substitui um administrador existente.

O cookie público chama-se `__Host-AxiomAtlas.Auth`, é `Secure`, `HttpOnly`, `SameSite=Lax`, sem domínio e com `Path=/`. A API recebe apenas Bearer token internamente; CORS não é habilitado na produção.
