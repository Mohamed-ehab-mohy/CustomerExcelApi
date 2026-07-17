using CustomerExcelApi.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CustomerExcelApi.Data.Configurations;

public sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Message).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.Status).HasConversion<int>();

        builder.HasIndex(r => new { r.Status, r.NextReminderTime })
            .HasDatabaseName("IX_Reminders_Status_NextReminderTime");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_Reminders_UserId");

        builder.HasIndex(r => new { r.UserId, r.Status })
            .HasDatabaseName("IX_Reminders_UserId_Status");
    }
}
