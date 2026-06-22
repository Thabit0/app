using DawishContentStudio.Core;
using Xunit;

namespace DawishContentStudio.Tests;

public sealed class MedicalClaimsGuardTests
{
    [Fact]
    public void BlocksMedicalClaims()
    {
        var guard = new MedicalClaimsGuard();
        Assert.False(guard.IsSafe("هذا المنتج مفيد للهضم ويقوي المناعة"));
    }

    [Fact]
    public void AllowsSafeMarketing()
    {
        var guard = new MedicalClaimsGuard();
        Assert.True(guard.IsSafe("اختيار مميز من الدويش، متوفر الآن للطلب من الموقع."));
    }
}
