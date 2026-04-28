using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ClientAccountApp
{
    public static class ClientContractService
    {
        public static void EnsureContractsForActiveOrganization()
        {
            ContractSchemaService.EnsureContractTables();

            var organization = ActiveOrganizationService.GetRequired();

            using var db = new AppDbContext();

            var clients = db.Clients
                .AsNoTracking()
                .ToList();

            foreach (var client in clients)
            {
                bool exists = db.ClientContracts.Any(x =>
                    x.OrganizationProfileId == organization.Id &&
                    x.ClientInfoId == client.Id);

                if (exists)
                    continue;

                db.ClientContracts.Add(new ClientContract
                {
                    OrganizationProfileId = organization.Id,
                    ClientInfoId = client.Id,
                    Status = "Требует договора",
                    ContractNumber = "",
                    DocumentRelativePath = "",
                    GeneratedAt = null,
                    SignedAt = null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            db.SaveChanges();
        }

        public static ClientContract GetOrCreateContract(AppDbContext db, int organizationId, int clientId)
        {
            var contract = db.ClientContracts.FirstOrDefault(x =>
                x.OrganizationProfileId == organizationId &&
                x.ClientInfoId == clientId);

            if (contract != null)
                return contract;

            contract = new ClientContract
            {
                OrganizationProfileId = organizationId,
                ClientInfoId = clientId,
                Status = "Требует договора",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.ClientContracts.Add(contract);
            db.SaveChanges();

            return contract;
        }
        public static int ResetContractsForActiveOrganizationToEmpty()
        {
            ContractSchemaService.EnsureContractTables();

            var organization = ActiveOrganizationService.GetRequired();

            using var db = new AppDbContext();

            var contracts = db.ClientContracts
                .Where(x => x.OrganizationProfileId == organization.Id)
                .ToList();

            foreach (var contract in contracts)
            {
                contract.Status = "Требует договора";
                contract.ContractNumber = "";
                contract.DocumentRelativePath = "";
                contract.GeneratedAt = null;
                contract.SignedAt = null;
                contract.UpdatedAt = DateTime.Now;
            }

            db.SaveChanges();

            return contracts.Count;
        }
        public static ClientContract? GetContract(AppDbContext db, int organizationId, int clientId)
        {
            return db.ClientContracts.FirstOrDefault(x =>
                x.OrganizationProfileId == organizationId &&
                x.ClientInfoId == clientId);
        }

        public static void MarkGenerated(
            AppDbContext db,
            ClientContract contract,
            string contractNumber,
            string documentRelativePath)
        {
            if (!string.Equals(contract.Status, "Договор подписан", StringComparison.OrdinalIgnoreCase))
            {
                contract.Status = "Договор сформирован";
            }

            contract.ContractNumber = contractNumber;
            contract.DocumentRelativePath = documentRelativePath;
            contract.GeneratedAt = DateTime.Now;
            contract.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }

        public static void MarkSigned(AppDbContext db, ClientContract contract)
        {
            if (!contract.GeneratedAt.HasValue)
            {
                contract.GeneratedAt = DateTime.Now;
            }

            contract.Status = "Договор подписан";
            contract.SignedAt = DateTime.Now;
            contract.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }
    }
}