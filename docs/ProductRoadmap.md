# Axiom Atlas - Product Roadmap

## Produto Atual
- Autenticacao e sessao Web com JWT/cookie.
- Gestao de usuarios, perfis e permissoes.
- Auditoria de operacoes relevantes.
- Configuracao de integracao OpenProject por ambiente.
- Apontamento de horas com Work Package, atividade, periodo, comentario e status de sincronizacao.
- Sincronizacao manual de apontamentos para o OpenProject.

## Proximas Features
- Observabilidade do sync: resumo por status, horas sincronizadas, pendencias e erros por usuario.
- Filtros de apontamentos: periodo, status, Work Package e atividade.
- Relatorios exportaveis: horas por projeto, usuario, periodo e status de sync.
- Retry assistido: reprocessar somente apontamentos com erro e exibir causa acionavel.
- Validacao operacional OpenProject: teste de conexao, atividades e permissao de lancamento antes do sync.
- Mapeamento de atividades: relacionar atividades internas com atividades retornadas pelo OpenProject.
- Dashboard inicial: cards reais do produto no lugar do template Metronic generico.
- Governanca administrativa: trilha de alteracoes de integracoes e usuarios.

## Feature 1 Em Implementacao
- Resumo operacional de apontamentos na tela de Time Entries.
- Objetivo: dar visibilidade imediata de horas totais, pendencias, sincronizados e erros.
- Escopo tecnico: endpoint autenticado na API, proxy no Web e cards de resumo na interface.
