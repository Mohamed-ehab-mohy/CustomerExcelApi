using CustomerExcelApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerExcelApi.Data.Configurations;

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Endpoint).IsRequired().HasMaxLength(500);
        builder.Property(s => s.P256dhKey).IsRequired().HasMaxLength(200);
        builder.Property(s => s.AuthKey).IsRequired().HasMaxLength(200);

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_PushSubscriptions_UserId");

        builder.HasIndex(s => new { s.UserId, s.Endpoint })
            .IsUnique()
            .HasDatabaseName("IX_PushSubscriptions_UserId_Endpoint");
    }
}
