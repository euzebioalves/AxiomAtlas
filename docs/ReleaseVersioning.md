# Versionamento e publicação

A `main` nunca recebe um commit automático de incremento de versão. A versão de uma release é derivada no workflow do SHA incorporado por merge: a maior tag estável `vMAJOR.MINOR.PATCH` é comparada ao `VersionPrefix` histórico e o próximo patch é calculado sem escrever arquivos.

`VersionPrefix` não precisa acompanhar cada release; ele é apenas baseline/fallback para repositórios sem tag mais nova. Durante a publicação, API, Web e Migrator recebem `Version`, `VersionPrefix`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`, `SourceRevisionId` e `Revision` por propriedades MSBuild. Assim, DLLs, ZIPs, metadados OCI e pacote de implantação correspondem à tag, embora o `.csproj` permaneça inalterado.

A tag `vX.Y.Z` é criada exatamente no `github.sha` que disparou o workflow. As imagens possuem somente as tags `X.Y.Z` e `sha-<commit>`, com labels OCI de versão e revisão; `latest` não é publicado. Antes de publicar, o workflow verifica se uma tag existente pertence ao mesmo SHA/revisão. Uma colisão com outro commit falha.

O release é preparado como rascunho: imagens e artefatos são construídos, ZIPs/tar/checksums são validados e só então os assets são enviados e o rascunho é publicado. Uma reexecução no mesmo commit reutiliza a tag, imagens e rascunho existentes. Isso permite concluir uma publicação parcial sem criar nova versão; uma release publicada já completa é apenas validada/reutilizada. Proteja `v*` no GitHub contra force update, alteração e exclusão.
