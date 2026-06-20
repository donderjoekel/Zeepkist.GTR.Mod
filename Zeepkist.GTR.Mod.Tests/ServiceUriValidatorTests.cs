using TNRD.Zeepkist.GTR.Configuration;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class ServiceUriValidatorTests
{
    [Theory]
    [InlineData("https://backend.zeepki.st")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    public void ParseBaseAddressAcceptsSecureAndLoopbackUrls(string value)
    {
        Uri result = ServiceUriValidator.ParseBaseAddress(value, "Test URL");

        Assert.Equal(new Uri(value), result);
    }

    [Theory]
    [InlineData("http://backend.zeepki.st")]
    [InlineData("ftp://backend.zeepki.st")]
    [InlineData("https://user:password@backend.zeepki.st")]
    [InlineData("not-a-url")]
    public void ParseBaseAddressRejectsUnsafeUrls(string value)
    {
        Assert.Throws<InvalidOperationException>(
            () => ServiceUriValidator.ParseBaseAddress(value, "Test URL"));
    }

    [Fact]
    public void ResolveCdnPathKeepsRelativePathOnConfiguredOrigin()
    {
        Uri result = ServiceUriValidator.ResolveCdnPath(
            "https://cdn.zeepki.st/base",
            "ghosts/record.bin");

        Assert.Equal("https://cdn.zeepki.st/base/ghosts/record.bin", result.AbsoluteUri);
    }

    [Fact]
    public void ResolveCdnPathAcceptsAbsoluteUrlOnConfiguredOrigin()
    {
        Uri result = ServiceUriValidator.ResolveCdnPath(
            "https://cdn.zeepki.st",
            "https://cdn.zeepki.st/ghosts/01KVKEMV7RHM741X7RNHGKCPGD.bin");

        Assert.Equal(
            "https://cdn.zeepki.st/ghosts/01KVKEMV7RHM741X7RNHGKCPGD.bin",
            result.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://attacker.example/ghost.bin")]
    [InlineData("http://cdn.zeepki.st/ghost.bin")]
    [InlineData("https://user:password@cdn.zeepki.st/ghost.bin")]
    [InlineData("//attacker.example/ghost.bin")]
    [InlineData("../ghost.bin")]
    [InlineData("")]
    public void ResolveCdnPathRejectsUntrustedPaths(string path)
    {
        Assert.Throws<InvalidOperationException>(
            () => ServiceUriValidator.ResolveCdnPath("https://cdn.zeepki.st", path));
    }
}
