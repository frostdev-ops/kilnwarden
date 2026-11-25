// SPDX-License-Identifier: MIT
using System.Text.Json.Nodes;
using Kilnwarden.Extensions;

namespace Kilnwarden.Offline.LaneC;

public sealed class JsonSchemaLiteTests
{
    private static readonly JsonObject Schema = (JsonObject)JsonNode.Parse("""
        {"type":"object","additionalProperties":false,"required":["name"],
         "properties":{"name":{"type":"string","minLength":1,"maxLength":5},"n":{"type":"integer","minimum":0,"maximum":10},
                       "mode":{"enum":["a","b"]},"tags":{"type":"array","maxItems":2,"items":{"type":"string"}},"on":{"type":"boolean"}}}
        """)!;

    [Theory]
    [InlineData("""{"name":"ab"}""", 0)]
    [InlineData("""{"name":"ab","n":3,"mode":"a","tags":["x"],"on":true}""", 0)]
    [InlineData("""{}""", 1)]
    [InlineData("""{"name":""}""", 1)]
    [InlineData("""{"name":"toolong"}""", 1)]
    [InlineData("""{"name":"a","n":3.5}""", 1)]
    [InlineData("""{"name":"a","n":11}""", 1)]
    [InlineData("""{"name":"a","mode":"c"}""", 1)]
    [InlineData("""{"name":"a","tags":["x","y","z"]}""", 1)]
    [InlineData("""{"name":"a","tags":[1]}""", 1)]
    [InlineData("""{"name":"a","extra":1}""", 1)]
    [InlineData("""[]""", 1)]
    public void Validates(string json, int problems) => Assert.Equal(problems, JsonSchemaLite.Validate(Schema, JsonNode.Parse(json)).Count);

    /// <summary>Regression (step 2 lab, settings lost on hot reload): Kiln reloads a mod on the game's tick thread, where
    /// the engine runs with SSE flush-to-zero / denormals-are-zero. The integer check compared d % 1 with double.Epsilon
    /// (a subnormal, read as 0 there), so every whole number failed "must be an integer". The checks must give the same
    /// answers in that mode, accepting whole numbers and still refusing fractions.</summary>
    [Fact]
    public void Integer_checks_hold_with_denormals_flushed()
    {
        Assert.SkipUnless(Support.FloatMode.Supported, "MXCSR control needs x64 on Linux or Windows");
        var problems = Support.FloatMode.WithDenormalsFlushed(() => new[]
        {
            JsonSchemaLite.Validate(Schema, JsonNode.Parse("""{"name":"a","n":3}""")).Count,
            JsonSchemaLite.Validate(Schema, JsonNode.Parse("""{"name":"a","n":0}""")).Count,
            JsonSchemaLite.Validate(Schema, JsonNode.Parse("""{"name":"a","n":3.5}""")).Count,
            JsonSchemaLite.Validate(Schema, JsonNode.Parse("""{"name":"a","n":3.0000001}""")).Count,
            JsonSchemaLite.Validate(Schema, JsonNode.Parse("""{"name":"a","n":1e-320}""")).Count,
        });
        Assert.Equal([0, 0, 1, 1, 1], problems);
        var mapped = Support.FloatMode.WithDenormalsFlushed(() => SettingsMapper.FromJson<Sample.SampleSettings>(new JsonObject { ["maxVisits"] = 50 }));
        Assert.Equal(50, mapped.MaxVisits);
        Assert.Throws<FormatException>(() => Support.FloatMode.WithDenormalsFlushed(() => SettingsMapper.FromJson<Sample.SampleSettings>(new JsonObject { ["maxVisits"] = 2.5 })));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(-0.0, true)]
    [InlineData(1.0, true)]
    [InlineData(-7.0, true)]
    [InlineData(50.0, true)]
    [InlineData(4503599627370496.0, true)]   // 2^52
    [InlineData(9007199254740993.0, true)]   // above 2^53 (rounds to a whole number)
    [InlineData(1e300, true)]
    [InlineData(0.5, false)]
    [InlineData(-2.25, false)]
    [InlineData(4503599627370495.5, false)]
    [InlineData(1e-320, false)]              // subnormal
    [InlineData(double.Epsilon, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    [InlineData(double.NegativeInfinity, false)]
    public void Whole_numbers_are_read_from_the_bits(double d, bool whole)
    {
        Assert.Equal(whole, JsonSchemaLite.IsWhole(d));
        if (Support.FloatMode.Supported) Assert.Equal(whole, Support.FloatMode.WithDenormalsFlushed(() => JsonSchemaLite.IsWhole(d)));
    }

    [Fact]
    public void Schemas_are_checked()
    {
        Assert.Empty(JsonSchemaLite.CheckSchema(Schema));
        Assert.NotEmpty(JsonSchemaLite.CheckSchema(new JsonObject { ["type"] = "date" }));
        Assert.NotEmpty(JsonSchemaLite.CheckSchema(new JsonObject { ["type"] = "string", ["pattern"] = "(a+)+$" }));
        Assert.NotEmpty(JsonSchemaLite.CheckSchema(new JsonObject { ["properties"] = new JsonArray() }));
        // deep nesting is refused, not recursed without end
        JsonObject deep = new() { ["type"] = "object" };
        var cur = deep;
        for (var i = 0; i < 40; i++)
        {
            var next = new JsonObject { ["type"] = "object" };
            cur["properties"] = new JsonObject { ["x"] = next };
            cur = next;
        }
        Assert.NotEmpty(JsonSchemaLite.CheckSchema(deep));
    }
}
