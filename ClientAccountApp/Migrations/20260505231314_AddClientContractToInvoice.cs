using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClientAccountApp.Migrations
{
    /// <inheritdoc />
    public partial class AddClientContractToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientType = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    DirectorFullName = table.Column<string>(type: "TEXT", nullable: true),
                    Inn = table.Column<string>(type: "TEXT", nullable: true),
                    Ogrn = table.Column<string>(type: "TEXT", nullable: true),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    ContractStatus = table.Column<string>(type: "TEXT", nullable: true),
                    ContractGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ContractSignedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    ShortName = table.Column<string>(type: "TEXT", nullable: true),
                    Inn = table.Column<string>(type: "TEXT", nullable: true),
                    Kpp = table.Column<string>(type: "TEXT", nullable: true),
                    Ogrn = table.Column<string>(type: "TEXT", nullable: true),
                    LegalAddress = table.Column<string>(type: "TEXT", nullable: true),
                    DirectorName = table.Column<string>(type: "TEXT", nullable: true),
                    DirectorPosition = table.Column<string>(type: "TEXT", nullable: true),
                    BankName = table.Column<string>(type: "TEXT", nullable: true),
                    BankBic = table.Column<string>(type: "TEXT", nullable: true),
                    SettlementAccount = table.Column<string>(type: "TEXT", nullable: true),
                    CorrespondentAccount = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    LogoRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServicesCatalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    DefaultVatRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServicesCatalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    BankName = table.Column<string>(type: "TEXT", nullable: true),
                    BIC = table.Column<string>(type: "TEXT", nullable: true),
                    CorrespondentAccount = table.Column<string>(type: "TEXT", nullable: true),
                    AccountNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BankAccounts_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClientFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientFiles_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientFiles_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClientNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    NoteText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReminderDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientNotes_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientNotes_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DigitalSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CertificationAuthority = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    IssuedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigitalSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DigitalSignatures_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClientContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrganizationProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrganizationProfileId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    ContractNumber = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContracts_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientContracts_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientContracts_OrganizationProfiles_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientContracts_OrganizationProfiles_OrganizationProfileId1",
                        column: x => x.OrganizationProfileId1,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClientRecurringServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceCatalogId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceCatalogId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceName = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    BillingCycle = table.Column<string>(type: "TEXT", nullable: true),
                    IsAdvanceBilling = table.Column<bool>(type: "INTEGER", nullable: false),
                    GenerateDay = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    LastGeneratedPeriodKey = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRecurringServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientRecurringServices_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientRecurringServices_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientRecurringServices_ServicesCatalog_ServiceCatalogId",
                        column: x => x.ServiceCatalogId,
                        principalTable: "ServicesCatalog",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClientRecurringServices_ServicesCatalog_ServiceCatalogId1",
                        column: x => x.ServiceCatalogId1,
                        principalTable: "ServicesCatalog",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientContractId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizationProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizationProfileId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "TEXT", nullable: true),
                    InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PeriodText = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", nullable: true),
                    TotalWithoutVat = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalWithVat = table.Column<decimal>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentRelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_ClientContracts_ClientContractId",
                        column: x => x.ClientContractId,
                        principalTable: "ClientContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Invoices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_OrganizationProfiles_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invoices_OrganizationProfiles_OrganizationProfileId1",
                        column: x => x.OrganizationProfileId1,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ClientInfoId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientInfoId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizationProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    OrganizationProfileId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    DocumentType = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentFormat = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_Clients_ClientInfoId",
                        column: x => x.ClientInfoId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_Clients_ClientInfoId1",
                        column: x => x.ClientInfoId1,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_Invoices_InvoiceId1",
                        column: x => x.InvoiceId1,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_OrganizationProfiles_OrganizationProfileId",
                        column: x => x.OrganizationProfileId,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceDocuments_OrganizationProfiles_OrganizationProfileId1",
                        column: x => x.OrganizationProfileId1,
                        principalTable: "OrganizationProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceCatalogId = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceCatalogId1 = table.Column<int>(type: "INTEGER", nullable: true),
                    ServiceName = table.Column<string>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    AmountWithoutVat = table.Column<decimal>(type: "TEXT", nullable: false),
                    VatAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AmountWithVat = table.Column<decimal>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_Invoices_InvoiceId1",
                        column: x => x.InvoiceId1,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_ServicesCatalog_ServiceCatalogId",
                        column: x => x.ServiceCatalogId,
                        principalTable: "ServicesCatalog",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoiceItems_ServicesCatalog_ServiceCatalogId1",
                        column: x => x.ServiceCatalogId1,
                        principalTable: "ServicesCatalog",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_ClientInfoId",
                table: "BankAccounts",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_ClientInfoId1",
                table: "BankAccounts",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContracts_ClientInfoId",
                table: "ClientContracts",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContracts_ClientInfoId1",
                table: "ClientContracts",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContracts_OrganizationProfileId",
                table: "ClientContracts",
                column: "OrganizationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContracts_OrganizationProfileId1",
                table: "ClientContracts",
                column: "OrganizationProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientFiles_ClientInfoId",
                table: "ClientFiles",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientFiles_ClientInfoId1",
                table: "ClientFiles",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotes_ClientInfoId",
                table: "ClientNotes",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientNotes_ClientInfoId1",
                table: "ClientNotes",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRecurringServices_ClientInfoId",
                table: "ClientRecurringServices",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRecurringServices_ClientInfoId1",
                table: "ClientRecurringServices",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRecurringServices_ServiceCatalogId",
                table: "ClientRecurringServices",
                column: "ServiceCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientRecurringServices_ServiceCatalogId1",
                table: "ClientRecurringServices",
                column: "ServiceCatalogId1");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_ClientInfoId",
                table: "DigitalSignatures",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_DigitalSignatures_ClientInfoId1",
                table: "DigitalSignatures",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_ClientInfoId",
                table: "InvoiceDocuments",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_ClientInfoId1",
                table: "InvoiceDocuments",
                column: "ClientInfoId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_InvoiceId",
                table: "InvoiceDocuments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_InvoiceId1",
                table: "InvoiceDocuments",
                column: "InvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_OrganizationProfileId",
                table: "InvoiceDocuments",
                column: "OrganizationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDocuments_OrganizationProfileId1",
                table: "InvoiceDocuments",
                column: "OrganizationProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId",
                table: "InvoiceItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_InvoiceId1",
                table: "InvoiceItems",
                column: "InvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ServiceCatalogId",
                table: "InvoiceItems",
                column: "ServiceCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceItems_ServiceCatalogId1",
                table: "InvoiceItems",
                column: "ServiceCatalogId1");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientContractId",
                table: "Invoices",
                column: "ClientContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId",
                table: "Invoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientInfoId",
                table: "Invoices",
                column: "ClientInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationProfileId",
                table: "Invoices",
                column: "OrganizationProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrganizationProfileId1",
                table: "Invoices",
                column: "OrganizationProfileId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "ClientFiles");

            migrationBuilder.DropTable(
                name: "ClientNotes");

            migrationBuilder.DropTable(
                name: "ClientRecurringServices");

            migrationBuilder.DropTable(
                name: "DigitalSignatures");

            migrationBuilder.DropTable(
                name: "InvoiceDocuments");

            migrationBuilder.DropTable(
                name: "InvoiceItems");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "ServicesCatalog");

            migrationBuilder.DropTable(
                name: "ClientContracts");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "OrganizationProfiles");
        }
    }
}
