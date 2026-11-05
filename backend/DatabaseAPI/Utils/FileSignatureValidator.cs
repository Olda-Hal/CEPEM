namespace DatabaseAPI.Utils;

/// <summary>
/// Magic-byte validation for uploaded files. Client-supplied Content-Type and
/// file extensions are not trusted.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] BmpSignature = [0x42, 0x4D];
    private static readonly byte[] TiffLittleEndian = [0x49, 0x49, 0x2A, 0x00];
    private static readonly byte[] TiffBigEndian = [0x4D, 0x4D, 0x00, 0x2A];
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] TdbSignature = "TOMODB v2.0"u8.ToArray();

    public static bool IsJpeg(ReadOnlySpan<byte> data) =>
        data.Length >= JpegSignature.Length && data[..JpegSignature.Length].SequenceEqual(JpegSignature);

    public static bool IsPng(ReadOnlySpan<byte> data) =>
        data.Length >= PngSignature.Length && data[..PngSignature.Length].SequenceEqual(PngSignature);

    public static bool IsGif(ReadOnlySpan<byte> data) =>
        (data.Length >= Gif87Signature.Length && data[..Gif87Signature.Length].SequenceEqual(Gif87Signature)) ||
        (data.Length >= Gif89Signature.Length && data[..Gif89Signature.Length].SequenceEqual(Gif89Signature));

    public static bool IsBmp(ReadOnlySpan<byte> data) =>
        data.Length >= BmpSignature.Length && data[..BmpSignature.Length].SequenceEqual(BmpSignature);

    public static bool IsTiff(ReadOnlySpan<byte> data) =>
        (data.Length >= TiffLittleEndian.Length && data[..TiffLittleEndian.Length].SequenceEqual(TiffLittleEndian)) ||
        (data.Length >= TiffBigEndian.Length && data[..TiffBigEndian.Length].SequenceEqual(TiffBigEndian));

    public static bool IsWebP(ReadOnlySpan<byte> data) =>
        data.Length >= 12 &&
        data[..4].SequenceEqual("RIFF"u8) &&
        data.Slice(8, 4).SequenceEqual("WEBP"u8);

    public static bool IsImage(ReadOnlySpan<byte> data) =>
        IsJpeg(data) || IsPng(data) || IsGif(data) || IsBmp(data) || IsTiff(data) || IsWebP(data);

    public static bool IsPdf(ReadOnlySpan<byte> data) =>
        data.Length >= PdfSignature.Length && data[..PdfSignature.Length].SequenceEqual(PdfSignature);

    public static bool IsTdb(ReadOnlySpan<byte> data) =>
        data.Length >= TdbSignature.Length && data[..TdbSignature.Length].SequenceEqual(TdbSignature);

    /// <summary>
    /// Patient photos are served as image/jpeg, so only real JPEG data is
    /// accepted. Other image formats would be stored but labelled incorrectly.
    /// </summary>
    public static string? ValidatePatientPhoto(ReadOnlySpan<byte> data) =>
        IsJpeg(data) ? null : "Photo must be a valid JPEG image.";

    /// <summary>A patient/visit document must be a real PDF or a real TDB measurement.</summary>
    public static string? ValidatePatientDocument(ReadOnlySpan<byte> data, bool isTdb) =>
        isTdb || IsPdf(data)
            ? null
            : "Document must be a PDF file or a valid MEIK .tdb measurement.";

    /// <summary>Examination attachments are PDF-only in the current UI.</summary>
    public static string? ValidateExaminationDocument(ReadOnlySpan<byte> data) =>
        IsPdf(data) ? null : "Examination document must be a PDF file.";
}
