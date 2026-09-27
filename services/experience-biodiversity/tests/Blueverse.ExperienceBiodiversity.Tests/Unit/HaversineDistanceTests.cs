using Xunit;

namespace Blueverse.ExperienceBiodiversity.Tests.Unit;

public sealed class HaversineDistanceTests
{
    private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var rLat1 = lat1 * Math.PI / 180.0;
        var rLat2 = lat2 * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(rLat1) * Math.Cos(rLat2) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return earthRadiusMeters * c;
    }

    [Fact]
    [Trait("CaseId", "EXP-GEO-001")]
    public void Same_Coordinates_Have_Zero_Distance()
    {
        var distance = CalculateHaversineDistanceMeters(5.9482, 80.4716, 5.9482, 80.4716);
        Assert.True(distance < 0.001);
    }

    [Fact]
    [Trait("CaseId", "EXP-GEO-002")]
    public void Distance_Between_Mirissa_And_Unawatuna_Is_Accurate()
    {
        // Mirissa: 5.9482 N, 80.4716 E
        // Unawatuna: 6.0108 N, 80.2497 E
        // Approximate distance is ~25.5 km (25,000 - 26,000 meters)
        var distance = CalculateHaversineDistanceMeters(5.9482, 80.4716, 6.0108, 80.2497);

        Assert.InRange(distance, 24000.0, 27000.0);
    }

    [Fact]
    [Trait("CaseId", "EXP-GEO-003")]
    public void Distance_Between_Mirissa_And_ArugamBay_Is_Within_Expected_Range()
    {
        // Mirissa: 5.9482 N, 80.4716 E
        // Arugam Bay: 6.8415 N, 81.8354 E
        // Approximate straight line distance ~180 km
        var distance = CalculateHaversineDistanceMeters(5.9482, 80.4716, 6.8415, 81.8354);

        Assert.InRange(distance, 170000.0, 195000.0);
    }
}
