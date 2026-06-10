using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class EfmigrationshistoryEntityTypeConfiguration : IEntityTypeConfiguration<Efmigrationshistory>
{
    public void Configure(EntityTypeBuilder<Efmigrationshistory> entity)
    {
        entity.ToTable("__efmigrationshistory");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.HasIndex(x => x.TenantId);
        entity.Property(x => x.TenantId).HasColumnName("TenantId");
        entity.Property(x => x.WorkflowInstanceId).HasColumnName("workflow_instance_id").HasMaxLength(36);
        entity.Property(x => x.ApprovalStatus).HasColumnName("approval_status").HasMaxLength(32).IsRequired();
        entity.HasIndex(x => x.WorkflowInstanceId);

        entity.Property(x => x.ProductVersion).HasColumnName("ProductVersion").HasMaxLength(32).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}