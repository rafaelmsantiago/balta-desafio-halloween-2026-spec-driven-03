using Encurtador.Application.Links;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Encurtador.Infrastructure.Persistence;

internal sealed class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    // Datas gravadas em UTC e lidas com Kind = Utc (research.md D6).
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("Links");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).IsRequired().HasMaxLength(20);
        // Garantia real de unicidade sob concorrência (research.md D1).
        builder.HasIndex(l => l.Code).IsUnique().HasDatabaseName("IX_Links_Code");

        builder.Property(l => l.DestinationUrl).IsRequired().HasMaxLength(2048);
        builder.Property(l => l.CreatedAt).IsRequired().HasConversion(UtcConverter);
        builder.Property(l => l.ExpiresAt).HasConversion(NullableUtcConverter);
        builder.Property(l => l.AccessCount).IsRequired();
        builder.Property(l => l.ManagementKeyHash).IsRequired().HasMaxLength(32);
    }
}
