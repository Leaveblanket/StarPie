using WinPieGestures;

namespace WinPieGestures.Tests;

public class SmokeTests
{
    [Fact]
    public void MainProject_IsReferenced_AndConfigModelIsConstructible()
    {
        Assert.Equal("StarPie", typeof(WinPieGestures.App).Assembly.GetName().Name);

        var config = new AppConfig();
        Assert.Equal(25.0, config.DragThreshold);
        Assert.Empty(config.Profiles);
    }
}
