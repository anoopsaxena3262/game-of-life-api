using GameOfLife.Api.Http;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>Path and query conversion, without a host. Expected values are the reference service's answers.</summary>
public sealed class SpringConversionsTests
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData(" 2", 2)]
    [InlineData("2 ", 2)]
    [InlineData("2 2", 22)]
    [InlineData("+2", 2)]
    [InlineData("010", 10)]
    [InlineData("0x2", 2)]
    [InlineData("0X2", 2)]
    [InlineData("#2", 2)]
    [InlineData("-0x1", -1)]
    [InlineData("0x10", 16)]
    [InlineData("1 50", 150)]
    [InlineData("0x7fffffff", int.MaxValue)]
    [InlineData("-0x80000000", int.MinValue)]
    public void Int_values_convert(string value, int expected)
    {
        Assert.Equal(expected, SpringConversions.ParseInt(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void A_blank_int_value_is_absent(string value)
    {
        Assert.Null(SpringConversions.ParseInt(value));
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("abc")]
    [InlineData("0x80000000")]
    [InlineData("2147483648")]
    [InlineData("0x")]
    [InlineData("0x-1")]
    [InlineData("--1")]
    public void Int_values_that_do_not_convert(string value)
    {
        Assert.Throws<FormatException>(() => SpringConversions.ParseInt(value));
    }

    [Theory]
    [InlineData("1-1-1-1-1", "00000001-0001-0001-0001-000000000001")]
    [InlineData("a-b-c-d-e", "0000000a-000b-000c-000d-00000000000e")]
    [InlineData("+1-1-1-1-1", "00000001-0001-0001-0001-000000000001")]
    [InlineData("123456789-1-1-1-1", "23456789-0001-0001-0001-000000000001")]
    [InlineData("1-12345-1-1-1", "00000001-2345-0001-0001-000000000001")]
    [InlineData("1-1-1-1-1234567890123", "00000001-0001-0001-0001-234567890123")]
    [InlineData("EA914E83-E538-4A30-98E3-F3DC13246FB6", "ea914e83-e538-4a30-98e3-f3dc13246fb6")]
    [InlineData(" ea914e83-e538-4a30-98e3-f3dc13246fb6 ", "ea914e83-e538-4a30-98e3-f3dc13246fb6")]
    public void Uuid_values_convert(string value, string expected)
    {
        Assert.Equal(Guid.Parse(expected), SpringConversions.ParseUuid(value));
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("-1-1-1-1-1")]
    [InlineData("1-1-1-1")]
    [InlineData("1-1-1-1-1-1")]
    [InlineData("1--1-1-1")]
    [InlineData("g-1-1-1-1")]
    [InlineData("0x1-1-1-1-1")]
    [InlineData("1-1-1-1-111111111111111111111111111111")]
    [InlineData("00000000000000000000000000000000")]
    [InlineData("{00000000-0000-0000-0000-000000000000}")]
    [InlineData("ea914e83-e538-4a30-98e3-f3dc13246fb6.json")]
    public void Uuid_values_that_do_not_convert(string value)
    {
        Assert.Throws<FormatException>(() => SpringConversions.ParseUuid(value));
    }
}
