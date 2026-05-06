using Microsoft.EntityFrameworkCore;
using System;

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
            InitializeDatabase();
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            Database.EnsureCreated();

            if (IsSqliteProvider())
            {
                EnsureClientStatusColumn();
                EnsureBankAccountCorrespondentAccountColumn();
                EnsureClientContractColumns();
                EnsureOgrnColumn();
                EnsureNoteReminderDateColumn(); // ← добавили
            }
        }
        private void EnsureNoteReminderDateColumn()
        {
            using var connection = Database.GetDbConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(ClientNotes);";

            bool hasReminderDate = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader["name"]?.ToString();

                    if (string.Equals(columnName, "ReminderDate", StringComparison.OrdinalIgnoreCase))
                    {
                        hasReminderDate = true;
                        break;
                    }
                }
            }

            if (!hasReminderDate)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE ClientNotes ADD COLUMN ReminderDate TEXT NULL;");
            }
        }

        private bool IsSqliteProvider()
        {
            string providerName = Database.ProviderName ?? "";

            return providerName.Contains(
                "Sqlite",
                StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureClientStatusColumn()
        {
            using var connection = Database.GetDbConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Clients);";

            bool hasStatus = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader["name"]?.ToString();

                    if (string.Equals(columnName, "Status", StringComparison.OrdinalIgnoreCase))
                    {
                        hasStatus = true;
                        break;
                    }
                }
            }

            if (!hasStatus)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Clients ADD COLUMN Status TEXT NOT NULL DEFAULT 'Активный';");
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SQL Server не любит каскадные удаления с несколькими путями.
            // Поэтому для корпоративного режима отключаем каскадное удаление у связей.

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
        private void EnsureClientContractColumns()
        {
            using var connection = Database.GetDbConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Clients);";

            bool hasContractStatus = false;
            bool hasContractGeneratedAt = false;
            bool hasContractSignedAt = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader["name"]?.ToString();

                    if (string.Equals(columnName, "ContractStatus", StringComparison.OrdinalIgnoreCase))
                        hasContractStatus = true;

                    if (string.Equals(columnName, "ContractGeneratedAt", StringComparison.OrdinalIgnoreCase))
                        hasContractGeneratedAt = true;

                    if (string.Equals(columnName, "ContractSignedAt", StringComparison.OrdinalIgnoreCase))
                        hasContractSignedAt = true;
                }
            }

            if (!hasContractStatus)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Clients ADD COLUMN ContractStatus TEXT NOT NULL DEFAULT 'Требует договора';");
            }

            if (!hasContractGeneratedAt)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Clients ADD COLUMN ContractGeneratedAt TEXT NULL;");
            }

            if (!hasContractSignedAt)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Clients ADD COLUMN ContractSignedAt TEXT NULL;");
            }
        }

        private void EnsureBankAccountCorrespondentAccountColumn()
        {
            using var connection = Database.GetDbConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(BankAccounts);";

            bool hasCorrespondentAccount = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader["name"]?.ToString();

                    if (string.Equals(columnName, "CorrespondentAccount", StringComparison.OrdinalIgnoreCase))
                    {
                        hasCorrespondentAccount = true;
                        break;
                    }
                }
            }

            if (!hasCorrespondentAccount)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE BankAccounts ADD COLUMN CorrespondentAccount TEXT NOT NULL DEFAULT '';");
            }
        }
        private void EnsureOgrnColumn()
        {
            using var connection = Database.GetDbConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Clients);";

            bool hasOgrn = false;

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var columnName = reader["name"]?.ToString();

                    if (string.Equals(columnName, "Ogrn", StringComparison.OrdinalIgnoreCase))
                    {
                        hasOgrn = true;
                        break;
                    }
                }
            }

            if (!hasOgrn)
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Clients ADD COLUMN Ogrn TEXT NOT NULL DEFAULT '';");
            }
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