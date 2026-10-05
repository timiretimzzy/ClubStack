namespace ClubStack.Application.Finance;

/// <summary>
/// The eight authoritative Phase 8 §14 financial posting rules (D6 PM Decision
/// Pack B7). The identities below are the documented §14.1-§14.8 rules; no new
/// rule identity may be introduced. Each constant carries its source section so
/// the mapping is auditable.
/// </summary>
public static class FinancialPostingRuleCodes
{
    /// <summary>Phase 8 §14.1 POS cash sale — Dr Cash / configured payment clearing account, Cr Sales revenue.</summary>
    public const string PosCashSale = "POS-CASH-SALE";

    /// <summary>Phase 8 §14.2 POS member-account charge — Dr Member receivable, Cr Sales revenue.</summary>
    public const string PosMemberAccountCharge = "POS-MEMBER-ACCOUNT-CHARGE";

    /// <summary>Phase 8 §14.3 Member account payment — Dr Cash / bank / configured payment account, Cr Member receivable.</summary>
    public const string MemberAccountPayment = "MEMBER-ACCOUNT-PAYMENT";

    /// <summary>Phase 8 §14.4 Membership fee assessment — Dr Member receivable, Cr Membership revenue.</summary>
    public const string MembershipFeeAssessment = "MEMBERSHIP-FEE-ASSESSMENT";

    /// <summary>Phase 8 §14.5 Inventory purchase — controlled inventory/expense and payable mapping.</summary>
    public const string InventoryPurchase = "INVENTORY-PURCHASE";

    /// <summary>Phase 8 §14.6 Supplier invoice — Dr Inventory / expense / configured account, Cr Accounts payable.</summary>
    public const string SupplierInvoice = "SUPPLIER-INVOICE";

    /// <summary>Phase 8 §14.7 Supplier payment — Dr Accounts payable, Cr Cash / bank.</summary>
    public const string SupplierPayment = "SUPPLIER-PAYMENT";

    /// <summary>Phase 8 §14.8 Refund — reverses the applicable revenue/receivable/payment effect.</summary>
    public const string Refund = "REFUND";
}

/// <summary>
/// How a rule turns an operational event into journal lines. <see cref="Direct"/>
/// rules build new lines from the rule's configured mappings;
/// <see cref="Reversal"/> rules (Phase 8 §14.8) are posted as the B4 compensating
/// journal that inverts the original journal's lines.
/// </summary>
public enum FinancialPostingStrategy
{
    Direct = 0,
    Reversal = 1,
}

/// <summary>
/// The amount-component selectors a posting rule mapping may declare (B7's
/// "optional selector"). A mapping whose selector is <c>null</c> receives the
/// request's single unnamed amount; a mapping with a selector receives the amount
/// supplied under the same selector. B18 is expressed with these: a sale mapping
/// set is Gross (debit clearing/AR), Net (credit revenue) and Tax (credit tax
/// payable); a purchase mapping set is Net + Tax (debits) and Gross (credit AP).
/// </summary>
public static class FinancialPostingSelectors
{
    /// <summary>Total amount including tax (B18 sales: "Dr Cash/AR gross").</summary>
    public const string Gross = "Gross";

    /// <summary>Amount excluding tax (B18: "Cr Revenue net", "Dr Expense/Inventory net").</summary>
    public const string Net = "Net";

    /// <summary>Tax portion (B18: "Cr Tax Payable tax", "Dr Input Tax").</summary>
    public const string Tax = "Tax";

    /// <summary>Prefix for routing a mapping by a source-specific key, e.g. <c>PaymentMethod:CASH</c>.</summary>
    public const string PaymentMethodPrefix = "PaymentMethod:";
}

/// <summary>The seeded definition of one Phase 8 §14 rule.</summary>
public sealed record FinancialPostingRuleSeed(
    string Code,
    string Name,
    string SourceSection,
    FinancialPostingStrategy Strategy,
    string Description);

/// <summary>
/// The authoritative Phase 8 §14 rule set, imported verbatim by the D6 seeder.
/// The mapping rows (account + sequence + direction + selector) are seeded
/// separately by <c>FinancialAccountSeeder</c> against the configured accounts.
/// </summary>
public static class FinancialPostingRules
{
    /// <summary>All eight rules in Phase 8 §14 document order.</summary>
    public static readonly IReadOnlyList<FinancialPostingRuleSeed> All =
    [
        new(
            FinancialPostingRuleCodes.PosCashSale,
            "POS cash sale",
            "Phase 8 14.1",
            FinancialPostingStrategy.Direct,
            "Debit Cash / configured payment clearing account; Credit Sales revenue. B18 splits the credit into Revenue net and Tax Payable tax."),
        new(
            FinancialPostingRuleCodes.PosMemberAccountCharge,
            "POS member-account charge",
            "Phase 8 14.2",
            FinancialPostingStrategy.Direct,
            "Debit Member receivable; Credit Sales revenue. B18 splits the credit into Revenue net and Tax Payable tax."),
        new(
            FinancialPostingRuleCodes.MemberAccountPayment,
            "Member account payment",
            "Phase 8 14.3",
            FinancialPostingStrategy.Direct,
            "Debit Cash / bank / configured payment account; Credit Member receivable."),
        new(
            FinancialPostingRuleCodes.MembershipFeeAssessment,
            "Membership fee assessment",
            "Phase 8 14.4",
            FinancialPostingStrategy.Direct,
            "Debit Member receivable; Credit Membership revenue."),
        new(
            FinancialPostingRuleCodes.InventoryPurchase,
            "Inventory purchase",
            "Phase 8 14.5",
            FinancialPostingStrategy.Direct,
            "Controlled inventory/expense and payable mapping. B18 splits the debits into Inventory/Expense net and Input Tax, credited to AP gross."),
        new(
            FinancialPostingRuleCodes.SupplierInvoice,
            "Supplier invoice",
            "Phase 8 14.6",
            FinancialPostingStrategy.Direct,
            "Debit Inventory / expense / configured account; Credit Accounts payable. B18 splits the debits into net expense and Input Tax, credited to AP gross."),
        new(
            FinancialPostingRuleCodes.SupplierPayment,
            "Supplier payment",
            "Phase 8 14.7",
            FinancialPostingStrategy.Direct,
            "Debit Accounts payable; Credit Cash / bank."),
        new(
            FinancialPostingRuleCodes.Refund,
            "Refund",
            "Phase 8 14.8",
            FinancialPostingStrategy.Reversal,
            "Reverses the applicable revenue/receivable/payment effect using a controlled reversal journal (B4: new compensating journal, original immutable)."),
    ];
}
