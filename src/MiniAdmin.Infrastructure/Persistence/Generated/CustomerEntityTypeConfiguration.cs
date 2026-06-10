using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class CustomerEntityTypeConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.ToTable("mini_customer");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.HasIndex(x => x.TenantId);
        entity.Property(x => x.TenantId).HasColumnName("TenantId");
        entity.Property(x => x.Title).HasColumnName("Title").HasMaxLength(256).IsRequired();
        entity.Property(x => x.Type).HasColumnName("Type").HasMaxLength(256).IsRequired();
        entity.Property(x => x.Content).HasColumnName("Content").HasMaxLength(256).IsRequired();
        entity.Property(x => x.IsPublished).HasColumnName("IsPublished").IsRequired();
        entity.Property(x => x.PublishedAt).HasColumnName("PublishedAt");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
