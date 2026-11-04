namespace RideSync.Web.Services;

public record VehicleType(string Id, string Name, string Icon, int Seats, double Base, double PerKm, double PerMin, double SpeedKmh, string Tagline);

public record FareOption(string Id, string Name, string Icon, int Seats, string Tagline, double Fare, int EtaMin);

/// Fare engine: fare = (base + perKm·km + perMin·min) × surge, with a minimum fare. Currency: BDT (৳).
public static class Pricing
{
    public static readonly IReadOnlyDictionary<string, VehicleType> Vehicles = new Dictionary<string, VehicleType>
    {
        ["bike"] = new("bike", "Bike", "🏍️", 1, 30, 12, 1, 26, "Beat the traffic"),
        ["car"]  = new("car",  "Car",  "🚗", 4, 50, 18, 2, 22, "Comfortable everyday rides"),
        ["xl"]   = new("xl",   "XL",   "🚙", 6, 80, 28, 3, 20, "Extra room for groups"),
    };

    private const double RoadFactor = 1.3;   // straight-line distance → realistic road distance
    private const double MinFare = 40;

    public static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1); var dLng = Rad(lng2 - lng1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    public static double RoadKm(double lat1, double lng1, double lat2, double lng2) => Math.Round(Haversine(lat1, lng1, lat2, lng2) * RoadFactor, 2);

    public static int DurationMin(VehicleType v, double km) => Math.Max(1, (int)Math.Ceiling(km / v.SpeedKmh * 60));

    public static double Fare(VehicleType v, double km, double surge) =>
        Math.Round(Math.Max(MinFare, (v.Base + v.PerKm * km + v.PerMin * DurationMin(v, km)) * surge), 0);

    /// Demand/supply multiplier: 1.0 when drivers ≥ requests; rises 0.25 per extra request per driver, capped at 2.0×.
    public static double Surge(int pendingRequests, int onlineDrivers)
    {
        var ratio = (pendingRequests + 1.0) / Math.Max(1, onlineDrivers);
        return ratio <= 1 ? 1.0 : Math.Min(2.0, Math.Round(1 + 0.25 * (ratio - 1), 1));
    }
}
