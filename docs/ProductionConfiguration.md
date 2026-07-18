# Configuracao de producao

O Axiom Atlas nao versiona credenciais. Configure os valores abaixo no provedor de hospedagem, no cofre de segredos ou em um `appsettings.Production.json` local, que e ignorado pelo Git.

## API

Defina as seguintes variaveis de ambiente para a API:

```text
ConnectionStrings__DefaultConnection=<connection string PostgreSQL>
JwtSettings__SecretKey=<segredo exclusivo com pelo menos 32 caracteres>
JwtSettings__Issuer=AxiomAtlasApi
JwtSettings__Audience=AxiomAtlasWeb
DataProtection__KeysPath=<diretorio persistente e restrito para as chaves>
```

`DataProtection__KeysPath` deve apontar para um volume persistente, compartilhado entre instancias da API quando houver escalonamento horizontal. Essas chaves protegem os tokens das integracoes cadastradas e devem ser preservadas em atualizacoes e reinicializacoes. Em desenvolvimento, o Axiom Atlas preserva o anel de chaves padrao do ASP.NET para manter legiveis as configuracoes locais ja criptografadas.

Os valores de e-mail seguem o mesmo padrao de variaveis hierarquicas, por exemplo `EmailSettings__Username` e `EmailSettings__Password`.

## Web

Configure a URL da API pela variavel:

```text
ApiSettings__BaseUrl=https://api.seu-dominio.example/
```

Em producao, o Web valida certificados TLS normalmente e o cookie de autenticacao exige HTTPS. A ignorancia de certificados autoassinados fica restrita ao ambiente `Development`.

## Publicacao segura

- Mantenha `ASPNETCORE_ENVIRONMENT=Production` na publicacao.
- Utilize apenas HTTPS entre navegador, Web e API.
- Restrinja permissao de leitura/escrita do diretorio de Data Protection a conta da aplicacao.
- Armazene senhas, tokens e connection strings em um cofre de segredos ou em variaveis protegidas pelo provedor de hospedagem.
- Execute os endpoints `GET /health/live` e `GET /health/ready` no monitoramento da aplicacao.
