using CarHub.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<VehicleModel> Models => Set<VehicleModel>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<AdminNotificationRead> AdminNotificationReads => Set<AdminNotificationRead>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<ProfessionalSubscription> ProfessionalSubscriptions => Set<ProfessionalSubscription>();
    public DbSet<ManualPaymentRequest> ManualPaymentRequests => Set<ManualPaymentRequest>();
    public DbSet<ManualPaymentDecision> ManualPaymentDecisions => Set<ManualPaymentDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.FullName).HasMaxLength(150);
            entity.Property(x => x.PhoneNumber).HasMaxLength(30);
            entity.Property(x => x.WhatsAppNumber).HasMaxLength(30);
            entity.Property(x => x.ProfileImageUrl).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.EmailConfirmationToken).HasMaxLength(300);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.ToTable("brands");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Slug).HasMaxLength(120);
        });

        modelBuilder.Entity<VehicleModel>(entity =>
        {
            entity.ToTable("models");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.BrandId, x.Slug }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.HasOne(x => x.Brand)
                .WithMany(x => x.Models)
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<City>(entity =>
        {
            entity.ToTable("cities");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Slug).HasMaxLength(120);
            entity.Property(x => x.Province).HasMaxLength(100);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Slug).HasMaxLength(120);
        });

        modelBuilder.Entity<Listing>(entity =>
        {
            entity.ToTable("listings");
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.BrandId);
            entity.HasIndex(x => x.ModelId);
            entity.HasIndex(x => x.CityId);
            entity.HasIndex(x => x.Price);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.PublishedAt);

            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.FuelType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.TransmissionType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.PhoneNumber).HasMaxLength(30);
            entity.Property(x => x.WhatsAppNumber).HasMaxLength(30);
            entity.Property(x => x.EngineSize).HasMaxLength(50);
            entity.Property(x => x.Color).HasMaxLength(50);
            entity.Property(x => x.Condition).HasMaxLength(50);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.Price).HasPrecision(18, 2);

            entity.HasOne(x => x.Seller)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Brand)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Model)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.ModelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.City)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Category)
                .WithMany(x => x.Listings)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ListingImage>(entity =>
        {
            entity.ToTable("listing_images");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Url).HasMaxLength(500);
            entity.Property(x => x.StorageKey).HasMaxLength(300);

            entity.HasOne(x => x.Listing)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.Property(x => x.Token).HasMaxLength(300);

            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<CompanyProfile>(entity =>
        {
            entity.ToTable("company_profiles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.CompanyName).HasMaxLength(180);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.TaxNumber).HasMaxLength(100);
            entity.Property(x => x.Address).HasMaxLength(240);
            entity.Property(x => x.ContactName).HasMaxLength(150);

            entity.HasOne(x => x.User)
                .WithOne(x => x.CompanyProfile)
                .HasForeignKey<CompanyProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProfessionalSubscription>(entity =>
        {
            entity.ToTable("professional_subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EndsAtUtc);
            entity.Property(x => x.PlanCode).HasMaxLength(60);
            entity.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.ExpiryReminderEmailSentAtUtc);
            entity.Property(x => x.ExpiryReminderSmsSentAtUtc);

            entity.HasOne(x => x.User)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.ToTable("admin_audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => x.EntityType);
            entity.HasIndex(x => x.EntityId);
            entity.HasIndex(x => x.AdminUserId);
            entity.Property(x => x.AdminEmail).HasMaxLength(200);
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(80);
        });


        modelBuilder.Entity<ManualPaymentRequest>(entity =>
        {
            entity.ToTable("manual_payment_requests");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ListingId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.InternalReference).IsUnique();
            entity.HasIndex(x => x.ProviderTransactionReference).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.InternalReference).HasMaxLength(80);
            entity.Property(x => x.ReceiverNumber).HasMaxLength(30);
            entity.Property(x => x.ProviderTransactionReference).HasMaxLength(120);
            entity.Property(x => x.SenderNumber).HasMaxLength(30);
            entity.Property(x => x.SenderName).HasMaxLength(150);
            entity.Property(x => x.ProofFileUrl).HasMaxLength(500);
            entity.Property(x => x.ProofFileHash).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ReviewNote).HasMaxLength(1000);
            entity.Property(x => x.ExpectedAmount).HasPrecision(18, 2);
            entity.Property(x => x.RequestedMonthlyPrice).HasPrecision(18, 2);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Listing)
                .WithMany()
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ManualPaymentDecision>(entity =>
        {
            entity.ToTable("manual_payment_decisions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PaymentRequestId);
            entity.HasIndex(x => x.AdminUserId);
            entity.Property(x => x.Action).HasMaxLength(60);
            entity.Property(x => x.Note).HasMaxLength(1000);

            entity.HasOne(x => x.PaymentRequest)
                .WithMany(x => x.Decisions)
                .HasForeignKey(x => x.PaymentRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AdminUser)
                .WithMany()
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<User>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<AdminNotificationRead>(entity =>
        {
            entity.ToTable("admin_notification_reads");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.AdminUserId, x.ListingId }).IsUnique();
            entity.Property(x => x.ReadAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(x => x.AdminUser)
                .WithMany()
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Listing)
                .WithMany()
                .HasForeignKey(x => x.ListingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Listing>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<ListingImage>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<RefreshToken>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<AdminAuditLog>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<AdminNotificationRead>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<CompanyProfile>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<ProfessionalSubscription>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<ManualPaymentRequest>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        modelBuilder.Entity<ManualPaymentDecision>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }

            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}







