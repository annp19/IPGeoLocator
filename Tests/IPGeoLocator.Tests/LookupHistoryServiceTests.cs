using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IPGeoLocator.Data;
using IPGeoLocator.Models;
using IPGeoLocator.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IPGeoLocator.Tests
{
    public class LookupHistoryServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly LookupHistoryService _service;

        public LookupHistoryServiceTests()
        {
            // Use in-memory database for testing
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;
            
            _context = new AppDbContext(options);
            _service = new LookupHistoryService(_context);
        }

        [Fact]
        public async Task AddLookupAsync_ValidLookup_AddsToDatabase()
        {
            // Arrange
            var lookup = new LookupHistory
            {
                IpAddress = "8.8.8.8",
                Country = "United States",
                City = "New York",
                LookupTime = DateTime.UtcNow
            };

            // Act
            await _service.AddLookupAsync(lookup);
            var allLookups = await _service.GetAllLookupsAsync();

            // Assert
            Assert.Single(allLookups);
            Assert.Equal("8.8.8.8", allLookups[0].IpAddress);
        }

        [Fact]
        public async Task GetRecentLookupsAsync_WithMultipleLookups_ReturnsCorrectCount()
        {
            // Arrange - Add multiple lookups
            var lookups = new List<LookupHistory>
            {
                new LookupHistory { IpAddress = "8.8.8.8", LookupTime = DateTime.UtcNow.AddMinutes(-5) },
                new LookupHistory { IpAddress = "1.1.1.1", LookupTime = DateTime.UtcNow.AddMinutes(-3) },
                new LookupHistory { IpAddress = "8.8.4.4", LookupTime = DateTime.UtcNow.AddMinutes(-1) }
            };

            foreach (var lookup in lookups)
            {
                await _service.AddLookupAsync(lookup);
            }

            // Act
            var recentLookups = await _service.GetRecentLookupsAsync(2);

            // Assert
            Assert.Equal(2, recentLookups.Count);
            // Most recent should be first
            Assert.Equal("8.8.4.4", recentLookups[0].IpAddress);
        }

        [Fact]
        public async Task SearchLookupsAsync_WithMatchingTerm_ReturnsMatchingResults()
        {
            // Arrange - Add test data
            var lookup1 = new LookupHistory { IpAddress = "192.168.1.1", City = "New York", Country = "United States" };
            var lookup2 = new LookupHistory { IpAddress = "10.0.0.1", City = "Los Angeles", Country = "United States" };
            
            await _service.AddLookupAsync(lookup1);
            await _service.AddLookupAsync(lookup2);

            // Act
            var results = await _service.SearchLookupsAsync("New York");

            // Assert
            Assert.Single(results);
            Assert.Equal("New York", results[0].City);
        }

        [Fact]
        public async Task DeleteLookupAsync_WithValidId_RemovesFromDatabase()
        {
            // Arrange
            var lookup = new LookupHistory { IpAddress = "8.8.8.8", City = "Test City" };
            await _service.AddLookupAsync(lookup);
            
            var allBefore = await _service.GetAllLookupsAsync();
            var lookupId = allBefore[0].Id; // Get the ID of the added lookup

            // Act
            await _service.DeleteLookupAsync(lookupId);
            var allAfter = await _service.GetAllLookupsAsync();

            // Assert
            Assert.Empty(allAfter);
        }

        [Fact]
        public async Task ClearAllLookupsAsync_RemovesAllEntries()
        {
            // Arrange - Add multiple lookups
            await _service.AddLookupAsync(new LookupHistory { IpAddress = "8.8.8.8" });
            await _service.AddLookupAsync(new LookupHistory { IpAddress = "1.1.1.1" });
            
            var allBefore = await _service.GetAllLookupsAsync();
            Assert.Equal(2, allBefore.Count);

            // Act
            await _service.ClearAllLookupsAsync();
            var allAfter = await _service.GetAllLookupsAsync();

            // Assert
            Assert.Empty(allAfter);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}