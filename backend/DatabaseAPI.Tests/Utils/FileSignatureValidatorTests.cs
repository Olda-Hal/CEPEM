using DatabaseAPI.Utils;
using Xunit;

namespace DatabaseAPI.Tests.Utils;

public class FileSignatureValidatorTests
{
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static byte[] Pdf() => "%PDF-1.7\n"u8.ToArray();
    private static byte[] Tdb() => "TOMODB v2.0 rest of the measurement"u8.ToArray();

    [Fact]
    public void RecognizesSupportedDocumentSignatures()
    {
        Assert.True(FileSignatureValidator.IsJpeg(Jpeg()));
        Assert.True(FileSignatureValidator.IsPdf(Pdf()));
        Assert.True(FileSignatureValidator.IsTdb(Tdb()));
        Assert.False(FileSignatureValidator.IsPdf(Jpeg()));
        Assert.False(FileSignatureValidator.IsTdb(Pdf()));
    }

    [Fact]
    public void PatientPhoto_RequiresJpeg()
    {
        Assert.Null(FileSignatureValidator.ValidatePatientPhoto(Jpeg()));
        Assert.NotNull(FileSignatureValidator.ValidatePatientPhoto(Pdf()));
        Assert.NotNull(FileSignatureValidator.ValidatePatientPhoto([0x89, 0x50, 0x4E, 0x47]));
    }

    [Fact]
    public void PatientDocument_RequiresPdfOrTdb()
    {
        Assert.Null(FileSignatureValidator.ValidatePatientDocument(Pdf(), isTdb: false));
        Assert.Null(FileSignatureValidator.ValidatePatientDocument(Tdb(), isTdb: true));
        Assert.NotNull(FileSignatureValidator.ValidatePatientDocument(Jpeg(), isTdb: false));
        Assert.NotNull(
            FileSignatureValidator.ValidatePatientDocument("plain text"u8.ToArray(), isTdb: false));
    }

    [Fact]
    public void ExaminationDocument_RequiresPdf()
    {
        Assert.Null(FileSignatureValidator.ValidateExaminationDocument(Pdf()));
        Assert.NotNull(FileSignatureValidator.ValidateExaminationDocument(Jpeg()));
    }
}
