using System.IO;

namespace CafePOS.Desktop.Services;

/// <summary>
/// Resolves the relative image paths returned by the API (for example
/// /uploads/product/photo.jpg) against the server that issued them.
/// </summary>
public static class MediaUrl
{
    private static string _serverOrigin = "http://localhost:5080";

    public static void SetServerUrl(string? apiBaseUrl) => SetApiBaseUrl(apiBaseUrl);

    public static void SetApiBaseUrl(string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl)) return;
        if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri)) return;
        _serverOrigin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var value = path.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            return absolute.ToString();

        if (File.Exists(value)) return new Uri(Path.GetFullPath(value)).AbsoluteUri;

        return _serverOrigin + "/" + value.TrimStart('/');
    }
}

public static class ImageDialog
{
    public const string Filter =
        "Images (*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp|All files (*.*)|*.*";
}
