using StarPie.Compatibility;

namespace StarPie.Tests;

/// <summary>
/// SDK ABI 政策缝：版本串解析与「同主版本、次版本不高于宿主」的兼容判定。
/// </summary>
public sealed class SdkAbiTests
{
    [Theory]
    [InlineData("1.0", true)]
    [InlineData(" 1.0 ", true)]
    [InlineData("2.0", true)]
    [InlineData("1", false)]
    [InlineData("1.0.0", false)]
    [InlineData("v1.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void 解析主次版本串_非法格式拒绝且输出归零(string? text, bool expected)
    {
        bool parsed = SdkAbi.TryParseVersion(text, out int major, out int minor);

        Assert.Equal(expected, parsed);
        if (!parsed)
        {
            Assert.Equal(0, major);
            Assert.Equal(0, minor);
        }
    }

    [Fact]
    public void 当前版本_为主次版本串()
    {
        Assert.Equal($"{SdkAbi.MajorVersion}.{SdkAbi.MinorVersion}", SdkAbi.Version);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 5, false)]
    [InlineData(0, 9, false)]
    [InlineData(2, 0, false)]
    public void 兼容判定_同主版本且次版本不高于宿主(int major, int minor, bool expected)
    {
        Assert.Equal(expected, SdkAbi.IsCompatibleWithCurrentHost(major, minor));
    }
}
