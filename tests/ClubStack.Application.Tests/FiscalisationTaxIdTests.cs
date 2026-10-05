using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Application.Tests;

/// <summary>
/// Tax identifiers are read from the device's own table rather than hard-coded. An
/// unregistered device returns an incoherent local snapshot, so resolution must fall
/// back to 0 (let the engine choose) whenever the table is ambiguous.
/// </summary>
public sealed class FiscalisationTaxIdTests
{
    private static readonly DeviceTaxBracket[] RegisteredDeviceTable =
    [
        new("515", "Standard Rated", 0.15m),
        new("516", "Zero Rated", 0m),
        new("517", "Exempt", 0m),
    ];

    [Fact]
    public void Registered_table_resolves_each_treatment_to_its_own_identifier()
    {
        Assert.Equal(515, TaxIds.ResolveStandardRated(RegisteredDeviceTable));
        Assert.Equal(516, TaxIds.ResolveZeroRated(RegisteredDeviceTable));
        Assert.Equal(517, TaxIds.ResolveExempt(RegisteredDeviceTable));
    }

    [Fact]
    public void Standard_rate_is_the_highest_positive_rate_in_the_table()
    {
        DeviceTaxBracket[] brackets =
        [
            new("510", "Reduced", 0.075m),
            new("511", "Standard", 0.15m),
            new("512", "Higher", 0.20m),
        ];

        Assert.Equal(512, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void Standard_rate_ignores_brackets_with_no_positive_rate()
    {
        DeviceTaxBracket[] brackets =
        [
            new("516", "Zero Rated", 0m),
            new("517", "Exempt", 0m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void Standard_rate_ignores_brackets_whose_identifier_is_not_a_number()
    {
        DeviceTaxBracket[] brackets =
        [
            new("not-a-number", "Standard Rated", 0.15m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void Standard_rate_ignores_a_bracket_whose_identifier_is_zero()
    {
        DeviceTaxBracket[] brackets =
        [
            new("0", "Standard Rated", 0.15m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void Zero_rate_requires_both_a_zero_rate_and_a_zero_named_bracket()
    {
        // Named "Zero Rated" but carries a non-zero rate: not a zero-rated bracket.
        DeviceTaxBracket[] mismatched =
        [
            new("900", "Zero Rated", 0.15m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveZeroRated(mismatched));
    }

    [Fact]
    public void Zero_rate_refuses_to_guess_when_the_table_lists_duplicate_zero_rated_brackets()
    {
        // Observed on an unregistered device: two entries both named "Zero Rated".
        DeviceTaxBracket[] ambiguous =
        [
            new("1", "Zero Rated", 0m),
            new("2", "Zero Rated", 0m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveZeroRated(ambiguous));
    }

    [Fact]
    public void Zero_rate_matches_a_single_bracket_even_when_other_zero_rate_brackets_exist()
    {
        DeviceTaxBracket[] brackets =
        [
            new("1", "Zero Rated", 0m),
            new("2", "Zero Rated", 0m),
            new("3", "Zero Rated", 0m),
        ];

        // Three distinct identifiers cannot be resolved safely.
        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveZeroRated(brackets));
    }

    [Fact]
    public void Zero_rate_is_case_insensitive()
    {
        DeviceTaxBracket[] brackets =
        [
            new("516", "ZERO RATED", 0m),
        ];

        Assert.Equal(516, TaxIds.ResolveZeroRated(brackets));
    }

    [Fact]
    public void Exempt_refuses_to_guess_when_the_table_lists_duplicate_exempt_brackets()
    {
        DeviceTaxBracket[] ambiguous =
        [
            new("7", "Exempt", 0m),
            new("8", "Exempt", 0m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveExempt(ambiguous));
    }

    [Fact]
    public void Exempt_does_not_match_a_zero_rated_bracket()
    {
        DeviceTaxBracket[] brackets =
        [
            new("516", "Zero Rated", 0m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveExempt(brackets));
    }

    [Fact]
    public void Repeated_identical_brackets_collapse_to_one_and_still_resolve()
    {
        // The local snapshot can list the same bracket more than once; duplicates of the
        // same identifier are not ambiguous, so resolution still succeeds.
        DeviceTaxBracket[] brackets =
        [
            new("516", "Zero Rated", 0m),
            new("516", "Zero Rated", 0m),
            new("517", "Exempt", 0m),
        ];

        Assert.Equal(516, TaxIds.ResolveZeroRated(brackets));
        Assert.Equal(517, TaxIds.ResolveExempt(brackets));
    }

    [Fact]
    public void An_empty_device_table_defers_every_treatment_to_the_engine()
    {
        DeviceTaxBracket[] empty = [];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(empty));
        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveZeroRated(empty));
        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveExempt(empty));
    }

    /// <summary>
    /// The tax table read from the development machine's FiscalStackZW configuration.
    /// Its only positive rate belongs to a bracket named "Zero Rated", and the bracket
    /// actually named "Standard" carries 0%.
    ///
    /// Selecting by rate alone tagged standard-rated lines with the zero-rated bracket,
    /// which would have written a wrong tax classification onto a ZIMRA document.
    /// </summary>
    private static readonly DeviceTaxBracket[] DevelopmentDeviceTable =
    [
        new("1", "Zero Rated", 0.155m),
        new("2", "Exempt", 0m),
        new("3", "Zero Rated", 0m),
        new("4", "Standard", 0m),
        new("5", "High", 0m),
        new("6", "None", 0m),
    ];

    [Fact]
    public void A_bracket_named_zero_rated_is_never_offered_as_the_standard_rate()
    {
        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(DevelopmentDeviceTable));
    }

    [Fact]
    public void The_development_device_table_still_resolves_its_zero_and_exempt_brackets()
    {
        // The exclusions above only affect standard-rated selection; the other two
        // treatments remain resolvable from the same table.
        Assert.Equal(3, TaxIds.ResolveZeroRated(DevelopmentDeviceTable));
        Assert.Equal(2, TaxIds.ResolveExempt(DevelopmentDeviceTable));
    }

    [Fact]
    public void A_bracket_named_exempt_is_never_offered_as_the_standard_rate()
    {
        DeviceTaxBracket[] brackets =
        [
            new("7", "Exempt", 0.18m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void The_highest_rate_still_wins_when_it_is_not_declared_zero_or_exempt()
    {
        DeviceTaxBracket[] brackets =
        [
            new("8", "Zero Rated", 0.50m),
            new("9", "Standard Rated", 0.15m),
            new("10", "Reduced", 0.075m),
        ];

        Assert.Equal(9, TaxIds.ResolveStandardRated(brackets));
    }

    [Fact]
    public void Two_identifiers_claiming_the_same_highest_rate_defers_to_the_engine()
    {
        DeviceTaxBracket[] brackets =
        [
            new("11", "Standard Rated", 0.15m),
            new("12", "Higher", 0.15m),
        ];

        Assert.Equal(TaxIds.EngineSelects, TaxIds.ResolveStandardRated(brackets));
    }
}
