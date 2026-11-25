using System.Reflection;
using System.Runtime.Versioning;
using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class TargetFrameworkTests
{
    [Fact]
    public void LoadsExpectedLibraryAsset()
    {
#if NET8_0
        const string expected = ".NETCoreApp,Version=v8.0";
#elif NET9_0
        const string expected = ".NETCoreApp,Version=v9.0";
#else
        const string expected = ".NETCoreApp,Version=v11.0";
#endif
        var framework = typeof(Sortz).Assembly.GetCustomAttribute<TargetFrameworkAttribute>();
        Assert.Equal(expected, framework?.FrameworkName);
    }

    [Fact]
    public void NullListThrowsArgumentNullException()
    {
        List<int> list = null!;
        Assert.Throws<ArgumentNullException>(() => list.Sortz());
    }
}
