using System;
using System.Threading.Tasks;
using IPGeoLocator.Tests;

namespace IPGeoLocator.ConsoleTests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("IP GeoLocator - Comprehensive Feature Verification");
            Console.WriteLine("==================================================");
            
            try
            {
                var tests = new ConsoleFeatureVerificationTests();
                await tests.RunAllTests();
                
                Console.WriteLine("\n🎉 All tests completed successfully!");
                Console.WriteLine("The IP GeoLocator application features are working correctly.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Error during testing: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            
            Console.WriteLine("\nTest execution completed.");
        }
    }
}