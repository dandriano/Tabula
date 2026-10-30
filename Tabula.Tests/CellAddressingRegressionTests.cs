using System;
using System.Collections.Generic;
using Tabula.CellAddressing;
using Xunit;

namespace Tabula.Tests;

public sealed class CellAddressingRegressionTests
{
    [Fact]
    public void CellAddrPreservesCoordinateEqualityHashLookupAndA1Formatting()
    {
        var first = new CellAddr(27, 4);
        var sameCoordinates = new CellAddr("AB5");
        var different = new CellAddr(26, 4);

        Assert.Equal(first, sameCoordinates);
        Assert.Equal(first.GetHashCode(), sameCoordinates.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.Equal("AB5", first.ToString());

        var lookup = new Dictionary<CellAddr, string> { [first] = "found" };
        Assert.Equal("found", lookup[sameCoordinates]);
    }

    [Fact]
    public void FullCellAddrEqualityIncludesSheetIdentityAndKeepsDisplayFormatting()
    {
        var workbook = new Workbook();
        var firstSheet = new Sheet(workbook, "Data", 3, 3);
        var secondSheet = new Sheet(workbook, "Data", 3, 3);
        var first = new FullCellAddr(firstSheet, new CellAddr("B3"));
        var sameAddressAndSheet = new FullCellAddr(firstSheet, 1, 2);
        var sameAddressDifferentSheet = new FullCellAddr(secondSheet, 1, 2);

        Assert.Equal(first, sameAddressAndSheet);
        Assert.Equal(first.GetHashCode(), sameAddressAndSheet.GetHashCode());
        Assert.NotEqual(first, sameAddressDifferentSheet);
        Assert.Equal("Data!B3", first.ToString());

        var lookup = new HashSet<FullCellAddr> { first };
        Assert.Contains(sameAddressAndSheet, lookup);
        Assert.DoesNotContain(sameAddressDifferentSheet, lookup);
    }

    [Fact]
    public void IntervalPreservesEqualityHashLookupAndConstructorValidation()
    {
        var interval = new Interval(-2, 4);
        var sameInterval = new Interval(-2, 4);
        Assert.Equal(interval, sameInterval);
        Assert.Equal(interval.GetHashCode(), sameInterval.GetHashCode());
        Assert.True(interval.Contains(0));

        var lookup = new HashSet<Interval> { interval };
        Assert.Contains(sameInterval, lookup);
        Assert.Throws<ArgumentException>(() => new Interval(5, 4));
    }
}
