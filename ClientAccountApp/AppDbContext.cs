using Microsoft.EntityFrameworkCore;

namespace ClientAccountApp
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientInfo> Clients { get; set; }
        public DbSet<DigitalSignature> DigitalSignatures { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<ClientNote> ClientNotes { get; set; }
        public DbSet<ClientFile> ClientFiles { get; set; }
        public DbSet<ServiceCatalog> ServicesCatalog => Set<ServiceCatalog>();
        public DbSet<ClientRecurringService> ClientRecurringServices { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }
        public DbSet<OrganizationProfile> OrganizationProfiles => Set<OrganizationProfile>();
        public DbSet<ClientContract> ClientContracts => Set<ClientContract>();
        public DbSet<InvoiceDocument> InvoiceDocuments => Set<InvoiceDocument>();

        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server не поддерживает каскадные удаления с несколькими путями —
            // отключаем для всех связей.

            modelBuilder.Entity<DigitalSignature>()
                .HasOne(x => x.ClientInfo)
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BankAccount>()
                .HasOne(x => x.ClientInfo)
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientNote>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientFile>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientRecurringService>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientRecurringService>()
                .HasOne<ServiceCatalog>()
                .WithMany()
                .HasForeignKey(x => x.ServiceCatalogId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Invoice>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Invoice>()
                .HasOne<OrganizationProfile>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationProfileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceItem>()
                .HasOne<ServiceCatalog>()
                .WithMany()
                .HasForeignKey(x => x.ServiceCatalogId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientContract>()
                .HasOne<OrganizationProfile>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationProfileId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ClientContract>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceDocument>()
                .HasOne<Invoice>()
                .WithMany()
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceDocument>()
                .HasOne<ClientInfo>()
                .WithMany()
                .HasForeignKey(x => x.ClientInfoId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InvoiceDocument>()
                .HasOne<OrganizationProfile>()
                .WithMany()
                .HasForeignKey(x => x.OrganizationProfileId)
                .OnDelete(DeleteBehavior.NoAction);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode == DatabaseProviderMode.SqlServer &&
                !string.IsNullOrWhiteSpace(settings.SqlServerConnectionString))
            {
                optionsBuilder.UseSqlServer(
                    settings.SqlServerConnectionString,
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure();
                    });

                return;
            }

            optionsBuilder.UseSqlite($"Data Source={AppPaths.DatabasePath}");
        }
    }
}