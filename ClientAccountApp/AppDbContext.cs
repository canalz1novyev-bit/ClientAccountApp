using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

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
            Database.EnsureCreated();
            EnsureClientStatusColumn();
            EnsureBankAccountCorrespondentAccountColumn();
            EnsureClientContractColumns();
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
                Database.ExecuteSqlRaw("ALTER TABLE Clients ADD COLUMN Status TEXT NOT NULL DEFAULT 'Активный';");
            }
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
                Database.ExecuteSqlRaw("ALTER TABLE BankAccounts ADD COLUMN CorrespondentAccount TEXT NOT NULL DEFAULT '';");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={AppPaths.DatabasePath}");
        }
    }
}