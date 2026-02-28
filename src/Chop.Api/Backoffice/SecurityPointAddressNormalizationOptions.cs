namespace Chop.Api.Backoffice;

public sealed class SecurityPointAddressNormalizationOptions
{
    public bool EnableGeocoding { get; set; }

    public string? YandexGeocoderApiKey { get; set; }
}

