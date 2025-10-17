using IPGeoLocator.Data;
using IPGeoLocator.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace IPGeoLocator.Services
{
    public interface ILookupHistoryService
    {
        Task AddLookupAsync(LookupHistory lookup);
        Task<List<LookupHistory>> GetRecentLookupsAsync(int count = 10);
        Task<List<LookupHistory>> GetAllLookupsAsync();
        Task<LookupHistory?> GetLookupByIdAsync(int id);
        Task<List<LookupHistory>> SearchLookupsAsync(string searchTerm);
        Task DeleteLookupAsync(int id);
        Task ClearAllLookupsAsync();
    }

    public class LookupHistoryService : ILookupHistoryService
    {
        private readonly AppDbContext _context;

        public LookupHistoryService(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddLookupAsync(LookupHistory lookup)
        {
            _context.LookupHistories.Add(lookup);
            await _context.SaveChangesAsync();
        }

        public async Task<List<LookupHistory>> GetRecentLookupsAsync(int count = 10)
        {
            return await _context.LookupHistories
                .OrderByDescending(l => l.LookupTime)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<LookupHistory>> GetAllLookupsAsync()
        {
            return await _context.LookupHistories
                .OrderByDescending(l => l.LookupTime)
                .ToListAsync();
        }

        public async Task<LookupHistory?> GetLookupByIdAsync(int id)
        {
            return await _context.LookupHistories.FindAsync(id);
        }

        public async Task<List<LookupHistory>> SearchLookupsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<LookupHistory>();
            }

            return await _context.LookupHistories
                .Where(l => 
                    l.IpAddress.Contains(searchTerm) ||
                    (l.City != null && l.City.Contains(searchTerm)) ||
                    (l.Country != null && l.Country.Contains(searchTerm)) ||
                    (l.Isp != null && l.Isp.Contains(searchTerm)) ||
                    (l.RegionName != null && l.RegionName.Contains(searchTerm))
                )
                .OrderByDescending(l => l.LookupTime)
                .ToListAsync();
        }

        public async Task DeleteLookupAsync(int id)
        {
            var lookup = await _context.LookupHistories.FindAsync(id);
            if (lookup != null)
            {
                _context.LookupHistories.Remove(lookup);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearAllLookupsAsync()
        {
            var allLookups = await _context.LookupHistories.ToListAsync();
            _context.LookupHistories.RemoveRange(allLookups);
            await _context.SaveChangesAsync();
        }
    }
}