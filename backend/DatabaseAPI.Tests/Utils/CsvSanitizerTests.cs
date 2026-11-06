using Cepem.Shared;
using Xunit;

namespace DatabaseAPI.Tests.Utils;

public class CsvSanitizerTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void PrefixesFormulaCells(string value)
    {
        Assert.Equal($"'{value}", CsvSanitizer.Cell(value));
    }

    [Fact]
    public void QuotesSeparatorsAndQuotes()
    {
        Assert.Equal("\"a;b\"", CsvSanitizer.Cell("a;b"));
        Assert.Equal("\"a\"\"b\"", CsvSanitizer.Cell("a\"b"));
    }

    [Fact]
    public void KeepsOrdinaryValues()
    {
        Assert.Equal("Jan Novák", CsvSanitizer.Cell("Jan Novák"));
        Assert.Equal(string.Empty, CsvSanitizer.Cell(null));
    }
}
