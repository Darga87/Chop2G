using NetTopologySuite.Geometries;

namespace Chop.Domain.Geo;

public static class GeoPointHelper
{
    private const int Srid = 4326;

    public static Point? Create(double? latitude, double? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        return new Point(longitude.Value, latitude.Value) { SRID = Srid };
    }

    public static (double? Latitude, double? Longitude) Read(Point? point, double? fallbackLatitude, double? fallbackLongitude)
    {
        if (point is null || point.IsEmpty)
        {
            return (fallbackLatitude, fallbackLongitude);
        }

        return (point.Y, point.X);
    }

    public static (double? Latitude, double? Longitude) Read(Point? point)
    {
        if (point is null || point.IsEmpty)
        {
            return (null, null);
        }

        return (point.Y, point.X);
    }
}
