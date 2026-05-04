using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

namespace ClientAccountApp
{
    public static class ActiveOrganizationService
    {
        private sealed class ActiveOrganizationState
        {
            public int? OrganizationProfileId { get; set; }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string StateFilePath =>
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "active-organization.json");

        public static int? CurrentOrganizationId { get; private set; }
        public static OrganizationProfile? Current { get; private set; }

        public static void Initialize()
        {
            OrganizationSchemaService.EnsureOrganizationTables();

            int? activeId = LoadActiveOrganizationId();

            if (!activeId.HasValue)
            {
                CurrentOrganizationId = null;
                Current = null;
                return;
            }

            using var db = new AppDbContext();

            var organization = db.OrganizationProfiles
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == activeId.Value && x.IsActive);

            if (organization == null)
            {
                CurrentOrganizationId = null;
                Current = null;
                SaveActiveOrganizationId(null);
                return;
            }

            CurrentOrganizationId = organization.Id;
            Current = organization;
        }

        public static List<OrganizationProfile> GetActiveOrganizations()
        {
            using var db = new AppDbContext();

            return db.OrganizationProfiles
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
        }

        public static bool HasAnyOrganizations()
        {
            using var db = new AppDbContext();

            return db.OrganizationProfiles
                .AsNoTracking()
                .Any(x => x.IsActive);
        }

        public static void SetActiveOrganization(int organizationId)
        {
            using var db = new AppDbContext();

            var organization = db.OrganizationProfiles
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == organizationId && x.IsActive);

            if (organization == null)
                throw new InvalidOperationException("Организация не найдена.");

            CurrentOrganizationId = organization.Id;
            Current = organization;

            SaveActiveOrganizationId(organization.Id);
        }
        public static void SetCurrentOrganization(int organizationId)
        {
            using var db = new AppDbContext();

            var organization = db.OrganizationProfiles.FirstOrDefault(x => x.Id == organizationId);

            if (organization == null)
                throw new InvalidOperationException("Организация не найдена.");

            SetActiveOrganization(organization);
        }
        public static void SetActiveOrganization(OrganizationProfile organization)
        {
            CurrentOrganizationId = organization.Id;
            Current = organization;

            SaveActiveOrganizationId(organization.Id);
        }

        public static void Clear()
        {
            CurrentOrganizationId = null;
            Current = null;
            SaveActiveOrganizationId(null);
        }

        public static OrganizationProfile GetRequired()
        {
            if (Current == null)
                throw new InvalidOperationException("Рабочая организация не выбрана.");

            return Current;
        }

        public static void RefreshCurrent()
        {
            if (!CurrentOrganizationId.HasValue)
                return;

            using var db = new AppDbContext();

            Current = db.OrganizationProfiles
                .AsNoTracking()
                .FirstOrDefault(x => x.Id == CurrentOrganizationId.Value && x.IsActive);
        }

        private static int? LoadActiveOrganizationId()
        {
            try
            {
                if (!File.Exists(StateFilePath))
                    return null;

                string json = File.ReadAllText(StateFilePath);
                var state = JsonSerializer.Deserialize<ActiveOrganizationState>(json, JsonOptions);

                return state?.OrganizationProfileId;
            }
            catch
            {
                return null;
            }
        }

        private static void SaveActiveOrganizationId(int? organizationId)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFilePath)!);

            var state = new ActiveOrganizationState
            {
                OrganizationProfileId = organizationId
            };

            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, JsonOptions));
        }
    }
}