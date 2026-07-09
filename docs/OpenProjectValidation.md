# OpenProject Validation

## 2026-07-07

### Homologation
- Status da configuracao: URL cadastrada e token cadastrado.
- Teste autenticado da API: executado via `POST /api/integrations/openproject/test`.
- Resultado: bloqueado antes da chamada ao OpenProject.
- Motivo: o token salvo nao pode ser descriptografado nesta instalacao.

### Diagnostico
- O banco possui um token criptografado para Homologation.
- A chave DataProtection atual nao consegue abrir esse payload.
- Isso costuma acontecer quando o banco foi copiado de outra pasta, usuario, maquina ou instalacao sem as chaves de DataProtection originais.

### Correcao Aplicada
- A API agora usa um nome de aplicacao DataProtection estavel: `Axiom.Atlas`.
- O servico OpenProject agora retorna uma mensagem operacional clara quando o token salvo nao pode ser descriptografado.

### Acao Necessaria
- Abrir a tela de Integrations > OpenProject.
- Reinformar o token da API em Homologation.
- Salvar as configuracoes.
- Executar novamente o teste de conexao.

Depois disso, o sistema deve conseguir validar `/api/v3/users/me`, carregar atividades e liberar o sync para um teste real de apontamento.
