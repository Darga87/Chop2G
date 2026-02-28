using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Chop.Api.Backoffice;

public interface ISecurityPointAddressNormalizer
{
    Task<SecurityPointAddressNormalizationResult> NormalizeAsync(
        string rawAddress,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken);
}

public sealed record SecurityPointAddressNormalizationResult(
    string Address,
    double? Latitude,
    double? Longitude);

public sealed partial class SecurityPointAddressNormalizer : ISecurityPointAddressNormalizer
{
    private readonly HttpClient _httpClient;
    private readonly SecurityPointAddressNormalizationOptions _options;

    public SecurityPointAddressNormalizer(
        HttpClient httpClient,
        IOptions<SecurityPointAddressNormalizationOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SecurityPointAddressNormalizationResult> NormalizeAsync(
        string rawAddress,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken)
    {
        var normalizedAddress = NormalizeAddress(rawAddress);
        if (latitude.HasValue || !_options.EnableGeocoding || string.IsNullOrWhiteSpace(_options.YandexGeocoderApiKey))
        {
            return new SecurityPointAddressNormalizationResult(normalizedAddress, latitude, longitude);
        }

        var geocoded = await TryGeocodeAsync(normalizedAddress, cancellationToken);
        if (geocoded is null)
        {
            return new SecurityPointAddressNormalizationResult(normalizedAddress, latitude, longitude);
        }

        var finalAddress = string.IsNullOrWhiteSpace(geocoded.Value.Address)
            ? normalizedAddress
            : NormalizeAddress(geocoded.Value.Address);

        return new SecurityPointAddressNormalizationResult(
            finalAddress,
            geocoded.Value.Latitude,
            geocoded.Value.Longitude);
    }

    public static string NormalizeAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = value.Trim();
        result = MultiSpaceRegex().Replace(result, " ");
        result = CommaSpaceRegex().Replace(result, ", ");
        result = DuplicatedCommaRegex().Replace(result, ",");
        return result.Trim(' ', ',');
    }

    private async Task<(string? Address, double Latitude, double Longitude)?> TryGeocodeAsync(
        string normalizedAddress,
        CancellationToken cancellationToken)
    {
        var uri =
            "https://geocode-maps.yandex.ru/1.x/?" +
            $"apikey={Uri.EscapeDataString(_options.YandexGeocoderApiKey!)}&" +
            "format=json&results=1&" +
            $"geocode={Uri.EscapeDataString(normalizedAddress)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        if (!root.TryGetProperty("response", out var responseNode)
            || !responseNode.TryGetProperty("GeoObjectCollection", out var collectionNode)
            || !collectionNode.TryGetProperty("featureMember", out var featureMemberNode)
            || featureMemberNode.ValueKind != JsonValueKind.Array
            || featureMemberNode.GetArrayLength() == 0)
        {
            return null;
        }

        var geoObject = featureMemberNode[0].GetProperty("GeoObject");
        var pointPos = geoObject.GetProperty("Point").GetProperty("pos").GetString();
        if (string.IsNullOrWhiteSpace(pointPos))
        {
            return null;
        }

        var parts = pointPos.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lon)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lat))
        {
            return null;
        }

        var address = geoObject
            .GetProperty("metaDataProperty")
            .GetProperty("GeocoderMetaData")
            .GetProperty("text")
            .GetString();

        return (address, lat, lon);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"\s*,\s*")]
    private static partial Regex CommaSpaceRegex();

    [GeneratedRegex(@",\s*,+")]
    private static partial Regex DuplicatedCommaRegex();
}

