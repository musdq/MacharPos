using Microsoft.AspNetCore.Http;

namespace MacharPos.Web.Services.Expenses;

public sealed record ExpenseEvidenceResult(
    bool IsValid,
    string? Error,
    byte[]? Data,
    string? FileName,
    string? ContentType);

public sealed class ExpenseEvidenceService
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    public async Task<ExpenseEvidenceResult> ValidateAndReadAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
            return Invalid("The selected evidence file is empty.");

        if (file.Length > MaxFileSize)
            return Invalid("Evidence must be 10 MB or smaller.");

        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        var data = stream.ToArray();

        var detected = DetectFileType(data);
        if (detected is null)
        {
            return Invalid(
                "Unsupported evidence file. Upload a JPEG, PNG, WebP, HEIC/HEIF image, or PDF.");
        }

        var safeFileName = BuildSafeFileName(file.FileName, detected.Value.Extension);
        return new ExpenseEvidenceResult(true, null, data, safeFileName, detected.Value.ContentType);
    }

    private static ExpenseEvidenceResult Invalid(string error)
        => new(false, error, null, null, null);

    private static (string Extension, string ContentType)? DetectFileType(byte[] data)
    {
        if (data.Length >= 5 && data[0] == (byte)'%' && data[1] == (byte)'P' &&
            data[2] == (byte)'D' && data[3] == (byte)'F' && data[4] == (byte)'-')
        {
            return (".pdf", "application/pdf");
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return (".jpg", "image/jpeg");

        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length >= pngSignature.Length && data.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature))
            return (".png", "image/png");

        if (data.Length >= 12 &&
            data.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            data.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return (".webp", "image/webp");
        }

        if (data.Length >= 12 && data.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(data, 8, 4).ToLowerInvariant();
            if (brand is "heic" or "heix" or "hevc" or "hevx")
                return (".heic", "image/heic");
            if (brand is "mif1" or "msf1")
                return (".heif", "image/heif");
        }

        return null;
    }

    private static string BuildSafeFileName(string untrustedName, string extension)
    {
        var baseName = Path.GetFileNameWithoutExtension(Path.GetFileName(untrustedName ?? ""));
        var safeChars = baseName
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            .Take(180)
            .ToArray();

        var safeBaseName = new string(safeChars).Trim('.', ' ', '-');
        if (string.IsNullOrWhiteSpace(safeBaseName))
            safeBaseName = "expense-proof";

        return safeBaseName + extension;
    }
}
