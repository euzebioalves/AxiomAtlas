using Axiom.Atlas.Domain.Entities.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Axiom.Atlas.Infrastructure.Configurations
{
    public class IntegrationSettingConfiguration : IEntityTypeConfiguration<IntegrationSettings>
    {
        public void Configure(EntityTypeBuilder<IntegrationSettings> builder)
        {
            // Nome da Tabela
            builder.ToTable("IntegrationSettings");

            // Chave Primária
            builder.HasKey(x => x.Id);

            // Propriedades Obrigatórias e Tamanhos
            builder.Property(x => x.Provider)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Environment)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            // Tokens: Como a nossa API vai criptografar esses dados usando o IDataProtectionProvider 
            // antes de salvar, a string resultante (Base64) fica consideravelmente maior que o 
            // token original. Por isso, 1000 caracteres é uma margem de segurança excelente.
            builder.Property(x => x.PrimaryToken)
                .HasMaxLength(1000);

            builder.Property(x => x.SecondaryToken)
                .HasMaxLength(1000);

            // Configurações Adicionais: Como receberá um JSON estruturado no futuro, 
            // usamos nvarchar(max) para não termos dor de cabeça com limite de tamanho.
            builder.Property(x => x.AdditionalSettings)
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.IsActive)
                .IsRequired();

            // A REGRA DE INTEGRIDADE DE OURO:
            // Criamos um índice único composto para garantir que o banco NUNCA aceite 
            // duas configurações de "Production" para o "OpenProject", por exemplo.
            builder.HasIndex(x => new { x.Provider, x.Environment })
                .IsUnique()
                .HasDatabaseName("IX_IntegrationSettings_Provider_Environment");
        }
    }
}
