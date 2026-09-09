using VoucherTracker.Api.Services;
using Xunit;

namespace VoucherTracker.Tests;

public class PinServiceTests
{
    [Fact]
    public void GeneratePin_ReturnsSixDigitString()
    {
        var service = new PinService();
        var pin = service.GeneratePin();

        Assert.Equal(6, pin.Length);
        Assert.True(int.TryParse(pin, out _), "PIN should be numeric");
    }

    [Fact]
    public void GeneratePin_PadsLeadingZeros()
    {
        // Run many times to increase the chance of hitting a PIN that would
        // otherwise lose its leading zero (e.g. 000123) if formatting were wrong.
        var service = new PinService();
        for (int i = 0; i < 200; i++)
        {
            var pin = service.GeneratePin();
            Assert.Equal(6, pin.Length);
        }
    }

    [Fact]
    public void GeneratePin_ProducesVariedValues()
    {
        var service = new PinService();
        var pins = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            pins.Add(service.GeneratePin());
        }

        // With 50 random 6-digit PINs, we expect essentially all of them to be unique.
        Assert.True(pins.Count > 45, "PINs should not be repeating suspiciously often");
    }
}
