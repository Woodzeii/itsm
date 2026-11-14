using FluentAssertions;
using itsm.Domain.Constants;

namespace itsm.Tests.UnitTests.Domain;

public class TicketNumberGeneratorTests
{
    [Fact]
    public void Generate_ProducesNumberMatchingMask()
    {
        for (var index = 0; index < 100; index++)
        {
            var number = TicketNumberGenerator.Generate();

            number.Should().HaveLength(TicketNumberGenerator.Mask.Length);
            TicketNumberGenerator.IsValid(number).Should().BeTrue(number);
            number.Should().MatchRegex("^[A-Z]{3}-[0-9]{3}-[0-9]{5}$");
        }
    }

    [Fact]
    public void Generate_ReturnsUpperCasedLetters()
    {
        var numbers = Enumerable.Range(0, 50).Select(_ => TicketNumberGenerator.Generate()).ToArray();

        numbers.Should().OnlyContain(number => number == number.ToUpperInvariant());
    }

    [Fact]
    public void GenerateUnique_SkipsTakenValues()
    {
        var taken = new HashSet<string>();
        while (taken.Count < 3)
            taken.Add(TicketNumberGenerator.Generate());

        var result = TicketNumberGenerator.GenerateUnique(taken.Contains);

        taken.Should().NotContain(result);
        TicketNumberGenerator.IsValid(result).Should().BeTrue();
    }

    [Fact]
    public void GenerateUnique_FailsWhenEveryValueIsTaken()
    {
        var attempts = 0;

        var act = () => TicketNumberGenerator.GenerateUnique(_ =>
        {
            attempts++;
            return true;
        });

        act.Should().Throw<InvalidOperationException>();
        attempts.Should().BeGreaterThan(1);
    }

    [Theory]
    [InlineData("GRM-663-22603", true)]
    [InlineData("VFT-104-99481", true)]
    [InlineData("grm-663-22603", false)]
    [InlineData("GRM-63-22603", false)]
    [InlineData("GRM1-663-22603", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ChecksMask(string? value, bool expected) =>
        TicketNumberGenerator.IsValid(value).Should().Be(expected);
}
