using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using EthangRss.Desktop.Views;

namespace EthangRss.Desktop.Services;

/// <summary>Caches remote images on disk so a scroll back up does not refetch them.</summary>
public sealed class ImageLoader : IImageLoader
{
    private readonly HttpClient _client = new();
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new();
    private readonly string _cacheDirectory;

    public ImageLoader()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EthangRss",
            "images");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public void Load(string url, Action<Bitmap> onLoaded)
    {
        if (_cache.TryGetValue(url, out var cached))
        {
            if (cached is not null)
            {
                onLoaded(cached);
            }

            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var path = Path.Combine(_cacheDirectory, Hash(url));
                byte[] bytes;

                if (File.Exists(path))
                {
                    bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
                }
                else
                {
                    using var response = await _client.GetAsync(url).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        _cache[url] = null;
                        return;
                    }

                    bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (bytes.Length is > 0 and < 8_000_000)
                    {
                        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                    }
                }

                await using var stream = new MemoryStream(bytes);
                var bitmap = new Bitmap(stream);
                _cache[url] = bitmap;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => onLoaded(bitmap));
            }
            catch (Exception)
            {
                _cache[url] = null;
            }
        });
    }

    private static string Hash(string url)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes)[..32];
    }
}
