using Microsoft.EntityFrameworkCore;
using IPGeoLocator.Models;
using System;
using System.IO;

namespace IPGeoLocator.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<LookupHistory> LookupHistories { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IPGeoLocator", "history.db");
                // Ensure the directory exists
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
                {
                    Directory.CreateDirectory(dbDir);
                }
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LookupHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
                entity.Property(e => e.LookupTime).IsRequired();
            });
        }
    }
}