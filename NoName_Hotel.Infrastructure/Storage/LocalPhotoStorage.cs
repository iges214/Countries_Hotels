using Microsoft.AspNetCore.Hosting;
using NoName_Hotel.Application.Contracts;

namespace NoName_Hotel.Infrastructure.Storage;

public sealed class LocalPhotoStorage(IWebHostEnvironment environment) : IPhotoStorage
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    private string DirectoryPath => Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");

    public async Task<string> SaveAsync(Stream source, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memory.Length + read > MaximumBytes) throw new ArgumentException("Images must be at most 5 MB.");
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var data = memory.ToArray();
        var extension = ImageExtension(data);
        if (extension is null) throw new ArgumentException("Upload a JPEG, PNG or WebP image.");
        Directory.CreateDirectory(DirectoryPath);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(DirectoryPath, name);
        try { await File.WriteAllBytesAsync(path, data, cancellationToken); }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return "/uploads/" + name;
    }

    public void Delete(string url)
    {
        if (!url.StartsWith("/uploads/", StringComparison.Ordinal)) return;
        var name = url[9..];
        if (name != Path.GetFileName(name) || name.Contains('\\') || name.Contains('/')) return;
        var path = Path.Combine(DirectoryPath, name);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string? ImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return ".jpg";
        if (bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return ".webp";
        return null;
    }
}
