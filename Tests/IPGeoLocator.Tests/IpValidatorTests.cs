using IPGeoLocator.Utilities;
using Xunit;

namespace IPGeoLocator.Tests
{
    public class IpValidatorTests
    {
        [Fact]
        public void IsValidIpAddress_ValidIPv4_ReturnsTrue()
        {
            // Arrange
            var validIp = "8.8.8.8";

            // Act
            var result = IpValidator.IsValidIpAddress(validIp);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidIpAddress_InvalidIPv4_ReturnsFalse()
        {
            // Arrange
            var invalidIp = "999.999.999.999";

            // Act
            var result = IpValidator.IsValidIpAddress(invalidIp);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidIpAddress_ValidIPv6_ReturnsTrue()
        {
            // Arrange
            var validIp = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";

            // Act
            var result = IpValidator.IsValidIpAddress(validIp);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidIpRange_ValidRange_ReturnsTrue()
        {
            // Arrange
            var validRange = "192.168.1.1-100";

            // Act
            var result = IpValidator.IsValidIpRange(validRange);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidIpRange_InvalidRange_ReturnsFalse()
        {
            // Arrange
            var invalidRange = "192.168.1.1-999"; // End number too high

            // Act
            var result = IpValidator.IsValidIpRange(invalidRange);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidCidr_ValidCidr_ReturnsTrue()
        {
            // Arrange
            var validCidr = "192.168.1.0/24";

            // Act
            var result = IpValidator.IsValidCidr(validCidr);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidCidr_InvalidCidr_ReturnsFalse()
        {
            // Arrange
            var invalidCidr = "192.168.1.0/33"; // Invalid prefix length

            // Act
            var result = IpValidator.IsValidCidr(invalidCidr);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateIpInput_ValidSingleIp_ReturnsCorrectResult()
        {
            // Arrange
            var input = "8.8.8.8";

            // Act
            var result = IpValidator.ValidateIpInput(input);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(IpInputType.SingleIp, result.Type);
            Assert.Equal(input, result.Normalized);
        }

        [Fact]
        public void ValidateIpInput_ValidRange_ReturnsCorrectResult()
        {
            // Arrange
            var input = "192.168.1.1-100";

            // Act
            var result = IpValidator.ValidateIpInput(input);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(IpInputType.IpRange, result.Type);
            Assert.Equal(input, result.Normalized);
        }

        [Fact]
        public void IsPrivateIpAddress_PrivateIp_ReturnsTrue()
        {
            // Arrange
            var privateIp = "192.168.1.1";

            // Act
            var result = IpValidator.IsPrivateIpAddress(privateIp);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsPrivateIpAddress_PublicIp_ReturnsFalse()
        {
            // Arrange
            var publicIp = "8.8.8.8";

            // Act
            var result = IpValidator.IsPrivateIpAddress(publicIp);

            // Assert
            Assert.False(result);
        }
    }
}