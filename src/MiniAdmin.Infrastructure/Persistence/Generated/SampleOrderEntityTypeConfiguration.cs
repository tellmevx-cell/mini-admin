using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class SampleOrderEntityTypeConfiguration : IEntityTypeConfiguration<SampleOrder>
{
    public void Configure(EntityTypeBuilder<SampleOrder> entity)
    {
        entity.ToTable("biz_sample_order");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.HasIndex(x => x.TenantId);
        entity.Property(x => x.TenantId).HasColumnName("TenantId");
        entity.HasIndex(x => x.WorkflowInstanceId);
        entity.Property(x => x.WorkflowInstanceId).HasColumnName("WorkflowInstanceId");

        entity.Property(x => x.OriginalName).HasColumnName("OriginalName").HasMaxLength(255).IsRequired();
        entity.Property(x => x.StoredName).HasColumnName("StoredName").HasMaxLength(255).IsRequired();
        entity.Property(x => x.ContentType).HasColumnName("ContentType").HasMaxLength(128).IsRequired();
        entity.Property(x => x.Size).HasColumnName("Size").IsRequired();
        entity.Property(x => x.StorageProvider).HasColumnName("StorageProvider").HasMaxLength(32).IsRequired();
        entity.Property(x => x.StoragePath).HasColumnName("StoragePath").HasMaxLength(512).IsRequired();
        entity.Property(x => x.Status).HasColumnName("Status").HasMaxLength(32).IsRequired();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
