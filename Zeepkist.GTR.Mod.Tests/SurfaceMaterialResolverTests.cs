using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class SurfaceKeyNormalizerTests
{
	[Theory]
	[InlineData("Wood (Instance)", "wood")]
	[InlineData("Ice", "ice")]
	[InlineData("Soap", "soap")]
	public void NormalizeSurfaceKey_NormalizesMaterialName(string name, string expected)
	{
		Assert.Equal(expected, SurfaceKeyNormalizer.NormalizeSurfaceKey(name));
	}

	[Fact]
	public void ChooseMostCommonSurfaceKey_ReturnsMajoritySurface()
	{
		string result = SurfaceKeyNormalizer.ChooseMostCommonSurfaceKey(
			new[] { "Wood", "Ice", "Wood (Instance)" });

		Assert.Equal("wood", result);
	}

	[Fact]
	public void ChooseMostCommonSurfaceKey_UsesFirstSeenForTie()
	{
		string result = SurfaceKeyNormalizer.ChooseMostCommonSurfaceKey(
			new[] { "Ice", "Wood" });

		Assert.Equal("ice", result);
	}

	[Fact]
	public void ChooseMostCommonSurfaceKey_IgnoresNullAndEmptyNames()
	{
		string result = SurfaceKeyNormalizer.ChooseMostCommonSurfaceKey(
			new[] { null, "", "  ", "Soap" });

		Assert.Equal("soap", result);
	}

	[Fact]
	public void ChooseMostCommonSurfaceKey_ReturnsNullWhenNoSurfaceExists()
	{
		string result = SurfaceKeyNormalizer.ChooseMostCommonSurfaceKey(
			new[] { null, "", "  " });

		Assert.Null(result);
	}
}
