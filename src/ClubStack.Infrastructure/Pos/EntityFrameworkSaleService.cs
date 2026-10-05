using ClubStack.Application.Accounts;
using ClubStack.Application.Credentials;
using ClubStack.Application.Exceptions;
using ClubStack.Application.Fiscalization;
using ClubStack.Application.Idempotency;
using ClubStack.Application.Outbox;
using ClubStack.Application.Pos;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Domain.Membership;
using ClubStack.Domain.Pos;
using ClubStack.Infrastructure.Fiscalization;
using ClubStack.Infrastructure.Inventory;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Pos;

/// <summary>
/// Point-of-sale sales (D5 POS). Sales are created on a register against an open
/// cashier session with prices resolved from the binding hierarchy (override,
/// member price, standard price); tax is snapshotted onto lines. Completion is
/// atomic and idempotent: stock is consumed, tenders are recorded (cash binds to
/// the cashier session drawer, account tenders charge the member account), a
/// receipt is generated and a financial event is enqueued. Completed sales are
/// immutable; corrections are refunds, which restore stock and credit member
/// accounts, never edits.
/// </summary>
public sealed class EntityFrameworkSaleService : ISaleService
{
    private const string AccountKind = "Account";
    private const string CashKind = "Cash";
    private const string ChargeReferencePrefix = "CHG";
    private const string CreditReferencePrefix = "CRT";
    private const string CompleteSaleOperation = "pos.sale.complete";
    private const string RefundSaleOperation = "pos.sale.refund";

    private readonly ClubStackDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;
    private readonly IIdempotencyStore _idempotency;
    private readonly IOutboxStore _outbox;
    private readonly ICredentialService _credentials;
    private readonly IMemberAccountService _accounts;
    private readonly StockLedgerWriter _ledger;
    private readonly IFiscalisationRequirementWriter _fiscalisation;

    public EntityFrameworkSaleService(
        ClubStackDbContext context,
        IUnitOfWork unitOfWork,
        IReferenceGenerator referenceGenerator,
        ISystemClock clock,
        IIdempotencyStore idempotency,
        IOutboxStore outbox,
        ICredentialService credentials,
        IMemberAccountService accounts,
        IFiscalisationRequirementWriter fiscalisation)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _referenceGenerator = referenceGenerator;
        _clock = clock;
        _idempotency = idempotency;
        _outbox = outbox;
        _credentials = credentials;
        _accounts = accounts;
        _fiscalisation = fiscalisation;
        _ledger = new StockLedgerWriter(context, clock);
    }

    public async Task<SaleDto> CreateAsync(CreateSaleInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (input.Lines.Count == 0)
        {
            throw new ValidationException(
                "The sale could not be created.",
                PosServiceHelpers.FieldError("lines", "At least one product line is required."));
        }

        foreach (var line in input.Lines)
        {
            PosServiceHelpers.EnsureGreaterThanZero(line.Quantity, "lines");
            if (line.UnitPriceOverride is { } priceOverride)
            {
                PosServiceHelpers.EnsureNotNegative(priceOverride, "lines");
            }
        }

        var register = await LoadRegisterAsync(input.RegisterId, cancellationToken);
        var session = await LoadOpenSessionAsync(register.Id, cancellationToken);
        var (memberId, memberNumber, memberName) = await ResolveMemberAsync(input.MemberId, input.CredentialValue, cancellationToken);

        var lineProductIds = input.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await LoadProductsAsync(lineProductIds, cancellationToken);
        foreach (var line in input.Lines)
        {
            if (products[line.ProductId].Status != ProductStatus.Active)
            {
                throw new ConflictException($"Product '{products[line.ProductId].Sku}' is not active.");
            }
        }

        var priceRows = await _context.ProductPrices
            .Where(p => lineProductIds.Contains(p.ProductId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var currencyCode = await ResolveCurrencyCodeAsync(cancellationToken);
        foreach (var line in input.Lines)
        {
            var product = products[line.ProductId];
            if (!string.Equals(product.CurrencyCode, currencyCode, StringComparison.Ordinal))
            {
                throw new ConflictException($"Product '{product.Sku}' is priced in '{product.CurrencyCode}' but the sale currency is '{currencyCode}'.");
            }
        }

        await EnsureStockAvailableAsync(input.Lines, products, register.InventoryLocationId, cancellationToken);

        var now = _clock.UtcNow;
        var businessDateUtc = input.BusinessDateUtc ?? now;
        var sale = new SaleEntity
        {
            SaleNumber = await _referenceGenerator.GenerateAsync(SaleNumber.Prefix, SaleNumber.Width, cancellationToken),
            Status = SaleStatus.Open,
            RegisterId = register.Id,
            PosOutletId = register.PosOutletId,
            LocationId = register.InventoryLocationId,
            CashierSessionId = session.Id,
            CashierUserId = actorUserId,
            MemberId = memberId,
            MemberNumber = memberNumber,
            MemberName = memberName,
            CurrencyCode = currencyCode,
            Note = PosServiceHelpers.NormalizeOptional(input.Note),
            BusinessDateUtc = businessDateUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        decimal subtotal = 0m, discountTotal = 0m, taxTotal = 0m, grandTotal = 0m;
        foreach (var line in input.Lines)
        {
            var product = products[line.ProductId];
            var (amount, _) = EntityFrameworkPosProductService.ResolveSellingPrice(product.Id, priceRows, memberId);
            var original = amount;
            var isOverride = line.UnitPriceOverride is not null;
            var effective = isOverride ? line.UnitPriceOverride!.Value : original;

            var computed = PosServiceHelpers.ComputeLinePricing(
                line.Quantity,
                original,
                effective,
                line.Discount,
                product.TaxTreatment,
                product.TaxRate?.Rate);

            var saleLine = new SaleLineEntity
            {
                ProductId = product.Id,
                ProductSku = product.Sku,
                ProductName = product.Name,
                StockManaged = product.StockManaged,
                Quantity = line.Quantity,
                OriginalUnitPrice = computed.OriginalUnitPrice,
                UnitPrice = computed.DiscountedUnitPrice,
                DiscountType = computed.DiscountType,
                DiscountValue = computed.DiscountValue,
                DiscountAmount = computed.DiscountAmount,
                TaxableBase = computed.TaxableBase,
                TaxAmount = computed.TaxAmount,
                LineTotal = computed.LineTotal,
                TaxTreatment = product.TaxTreatment,
                TaxRateName = product.TaxRate?.Name,
                TaxRate = product.TaxRate?.Rate,
                CreatedAtUtc = now,
            };
            sale.Lines.Add(saleLine);

            if (line.Discount is not null)
            {
                sale.Discounts.Add(new SaleDiscountEntity
                {
                    SaleId = sale.Id,
                    Level = "Line",
                    LineId = saleLine.Id,
                    DiscountType = computed.DiscountType!.Value,
                    Value = computed.DiscountValue!.Value,
                    Reason = PosServiceHelpers.NormalizeOptional(line.Discount.Reason),
                    AuthorizedByUserId = actorUserId,
                    AppliedAtUtc = now,
                });
            }

            subtotal += computed.TaxableBase;
            discountTotal += computed.DiscountAmount;
            taxTotal += computed.TaxAmount;
            grandTotal += computed.LineTotal;
        }

        sale.Subtotal = PosPricing.Money(subtotal);
        sale.DiscountTotal = PosPricing.Money(discountTotal);
        sale.TaxTotal = PosPricing.Money(taxTotal);
        sale.GrandTotal = PosPricing.Money(grandTotal);

        _context.Sales.Add(sale);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadSaleAsync(sale.Id, tracked: false, cancellationToken);
        return PosMappings.ToSaleDto(created);
    }

    public async Task<SaleDto> CompleteAsync(Guid id, CompleteSaleInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (input.Payments.Count == 0)
        {
            throw new ValidationException(
                "The sale could not be completed.",
                PosServiceHelpers.FieldError("payments", "At least one payment is required."));
        }

        foreach (var payment in input.Payments)
        {
            PosServiceHelpers.EnsureGreaterThanZero(payment.Amount, "payments");
        }

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(input.IdempotencyKey);
        // The target sale is part of the operation's identity: without it, the same key
        // with the same tenders against a different sale would replay the wrong result.
        var requestHash = idempotencyKey is null
            ? null
            : IdempotencyRequestHash.Compute($"{id:N}|{PosJson.Serialize(input)}");
        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey, CompleteSaleOperation, actorUserId, requestHash, correlationId, cancellationToken);
            if (!created)
            {
                return ReplayStoredOrConflict<SaleDto>(existing, "sale completion", requestHash);
            }
        }

        try
        {
            return await CompleteSaleCoreAsync(id, input, actorUserId, correlationId, idempotencyKey, cancellationToken);
        }
        catch
        {
            // Any failure after the key was claimed — including validation and state
            // conflicts raised before the transaction opened — must release the key so
            // the same request can be retried instead of staying wedged InProgress.
            await MarkIdempotencyFailedAsync(idempotencyKey, CompleteSaleOperation, actorUserId, "The sale completion failed and was rolled back.", cancellationToken);
            throw;
        }
    }

    private async Task<SaleDto> CompleteSaleCoreAsync(
        Guid id,
        CompleteSaleInput input,
        string actorUserId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var sale = await LoadSaleAsync(id, tracked: true, cancellationToken);
        if (sale.Status is SaleStatus.Completed or SaleStatus.Voided)
        {
            throw new ConflictException($"Sale '{sale.SaleNumber}' is already {sale.Status.ToString().ToLowerInvariant()}.");
        }

        if (!SaleStateTransitions.CanTransition(sale.Status, SaleStatus.PendingPayment))
        {
            throw new ConflictException($"Sale '{sale.SaleNumber}' cannot be completed from its '{sale.Status}' state.");
        }

        if (input.MemberId is { } requestedMember && sale.MemberId is { } existingMember && existingMember != requestedMember)
        {
            throw new ConflictException("The sale already belongs to a different member.");
        }

        var (memberId, memberNumber, memberName) = await ResolveMemberAsync(input.MemberId ?? sale.MemberId, null, cancellationToken);

        var register = await LoadRegisterAsync(sale.RegisterId, cancellationToken);
        var session = await LoadOpenSessionAsync(register.Id, cancellationToken);
        var resolvedPayments = await ResolvePaymentsAsync(input.Payments, cancellationToken);

        var total = PosPricing.Money(resolvedPayments.Sum(p => p.Input.Amount));
        if (total != PosPricing.Money(sale.GrandTotal))
        {
            throw new ValidationException(
                "The sale could not be completed.",
                PosServiceHelpers.FieldError("payments", $"Payments total {total:F2} but the sale total is {sale.GrandTotal:F2}."));
        }

        foreach (var payment in resolvedPayments)
        {
            if (string.Equals(payment.Method.Kind, AccountKind, StringComparison.Ordinal) && memberId is null)
            {
                throw new ValidationException(
                    "The sale could not be completed.",
                    PosServiceHelpers.FieldError("payments", "Account payments require a member."));
            }
        }

        var stockLines = sale.Lines.Where(l => l.StockManaged).ToList();
        var products = await LoadProductsAsync(stockLines.Select(l => l.ProductId).Distinct().ToList(), cancellationToken);

        // Resolve (creating on first use) the member account before the transaction so the
        // account-number allocation and its save never run on the transactional connection.
        Guid? memberAccountId = null;
        if (memberId is { } accountMemberId
            && resolvedPayments.Any(p => string.Equals(p.Method.Kind, AccountKind, StringComparison.Ordinal)))
        {
            memberAccountId = (await _accounts.GetAccountForMemberAsync(accountMemberId, cancellationToken)).Id;
        }

        var receiptNumber = await _referenceGenerator.GenerateAsync(ReceiptNumber.Prefix, ReceiptNumber.Width, cancellationToken);
        var movementReferences = await AllocateManyAsync(StockMovementNumber.Prefix, stockLines.Count, cancellationToken);
        var chargeReferences = await AllocateChargeReferencesAsync(resolvedPayments, cancellationToken);

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            if (sale.Status == SaleStatus.Open)
            {
                sale.Status = SaleStatus.PendingPayment;
            }

            if (session.Status == CashierSessionStatus.Open)
            {
                session.Status = CashierSessionStatus.Active;
                session.StartedAtUtc = _clock.UtcNow;
            }

            if (sale.MemberId is null && memberId is { } resolvedMemberId)
            {
                sale.MemberId = resolvedMemberId;
                sale.MemberNumber = memberNumber;
                sale.MemberName = memberName;
            }

            if (!SaleStateTransitions.CanTransition(sale.Status, SaleStatus.Completed))
            {
                throw new ConflictException($"Sale '{sale.SaleNumber}' cannot be completed from its '{sale.Status}' state.");
            }

            if (stockLines.Count > 0)
            {
                var lockKeys = stockLines.Select(l => (l.ProductId, sale.LocationId)).ToList();
                await StockLedgerWriter.AcquireLocksAsync(_context, lockKeys, cancellationToken);
            }

            for (var i = 0; i < stockLines.Count; i++)
            {
                var line = stockLines[i];
                var product = products[line.ProductId];
                await _ledger.ApplyAsync(
                    StockMovementType.Sale,
                    line.ProductId,
                    sale.LocationId,
                    line.Quantity,
                    product.Cost,
                    sale.CurrencyCode,
                    sale.BusinessDateUtc,
                    actorUserId,
                    reason: null,
                    PosMovementSources.Sale,
                    sale.Id,
                    movementReferences[i],
                    cancellationToken);
            }

            for (var i = 0; i < resolvedPayments.Count; i++)
            {
                var resolved = resolvedPayments[i];
                var payment = new SalePaymentEntity
                {
                    SaleId = sale.Id,
                    PaymentMethodId = resolved.Method.Id,
                    PaymentMethodCode = resolved.Method.Code,
                    PaymentMethodName = resolved.Method.Name,
                    Kind = resolved.Method.Kind,
                    Amount = resolved.Input.Amount,
                    IsRefund = false,
                    ExternalReference = PosServiceHelpers.NormalizeOptional(resolved.Input.ExternalReference),
                    CreatedAtUtc = _clock.UtcNow,
                };

                if (string.Equals(resolved.Method.Kind, CashKind, StringComparison.Ordinal))
                {
                    payment.CashierSessionId = session.Id;
                }

                if (string.Equals(resolved.Method.Kind, AccountKind, StringComparison.Ordinal) && memberAccountId is { } accountId)
                {
                    var charge = await _accounts.ChargeAsync(accountId, new ChargeInput(
                        $"POS sale {sale.SaleNumber}",
                        resolved.Input.Amount,
                        sale.CurrencyCode,
                        chargeReferences[i],
                        SourceType: "PosSale",
                        SourceId: sale.Id,
                        sale.BusinessDateUtc), actorUserId, cancellationToken);
                    payment.AccountTransactionId = charge.Id;
                    payment.AccountTransactionReference = charge.Reference;
                }

                // Add through the context so the row is tracked as Added; relying on
                // navigation-collection fixup alone promotes the pre-keyed child to
                // Modified and emits an UPDATE that matches zero rows.
                _context.SalePayments.Add(payment);
            }

            sale.Status = SaleStatus.Completed;
            sale.CompletedAtUtc = _clock.UtcNow;
            sale.UpdatedAtUtc = _clock.UtcNow;

            _context.Receipts.Add(new ReceiptEntity
            {
                ReceiptNumber = receiptNumber,
                SaleId = sale.Id,
                SaleNumber = sale.SaleNumber,
                Status = "Issued",
                RegisterId = sale.RegisterId,
                RegisterCode = register.Code,
                PosOutletId = sale.PosOutletId,
                PosOutletCode = register.PosOutlet.Code,
                CashierUserId = actorUserId,
                MemberId = sale.MemberId,
                MemberNumber = sale.MemberNumber,
                MemberName = sale.MemberName,
                CurrencyCode = sale.CurrencyCode,
                Subtotal = sale.Subtotal,
                DiscountTotal = sale.DiscountTotal,
                TaxTotal = sale.TaxTotal,
                GrandTotal = sale.GrandTotal,
                BusinessDateUtc = sale.BusinessDateUtc,
                GeneratedAtUtc = sale.CompletedAtUtc!.Value,
            });

            await _outbox.EnqueueAsync(
                PosEventTypes.SaleFinancialEvent,
                PosJson.Serialize(new SaleFinancialEvent(
                    sale.Id,
                    sale.SaleNumber,
                    sale.PosOutletId,
                    sale.RegisterId,
                    sale.LocationId,
                    actorUserId,
                    sale.MemberId,
                    sale.CurrencyCode,
                    sale.Subtotal,
                    sale.DiscountTotal,
                    sale.TaxTotal,
                    sale.GrandTotal,
                    sale.Payments.Where(p => !p.IsRefund).Select(PosMappings.ToSalePaymentDto).ToList(),
                    sale.BusinessDateUtc,
                    sale.CompletedAtUtc!.Value,
                    correlationId)),
                correlationId,
                cancellationToken);

            // D5.5: the sale now owes a fiscal document. Recording the requirement and
            // its outbox message happens inside this same transaction, so the document
            // can never be lost by a crash after commit. The device is contacted later
            // by the outbox worker, never here.
            await _fiscalisation.RequireAsync(sale, refund: null, correlationId, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);

            var completed = await LoadSaleAsync(id, tracked: false, cancellationToken);
            var dto = PosMappings.ToSaleDto(completed);
            if (idempotencyKey is not null)
            {
                try
                {
                    await _idempotency.CompleteAsync(idempotencyKey, CompleteSaleOperation, actorUserId, PosJson.Serialize(dto), cancellationToken);
                }
                catch (Exception)
                {
                    await _idempotency.FailAsync(idempotencyKey, CompleteSaleOperation, actorUserId, "Idempotency completion failed", cancellationToken);
                }
            }

            return dto;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SaleDto> VoidAsync(Guid id, VoidSaleInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var reason = PosServiceHelpers.NormalizeOptional(input.Reason);
        if (reason is null)
        {
            throw new ValidationException(
                "The sale could not be voided.",
                PosServiceHelpers.FieldError("reason", "A reason for voiding the sale is required."));
        }

        var sale = await LoadSaleAsync(id, tracked: true, cancellationToken);
        if (!SaleStateTransitions.CanTransition(sale.Status, SaleStatus.Voided))
        {
            throw new ConflictException($"Sale '{sale.SaleNumber}' cannot be voided from its '{sale.Status}' state; completed sales require a refund.");
        }

        var now = _clock.UtcNow;
        sale.Status = SaleStatus.Voided;
        sale.VoidedAtUtc = now;
        sale.VoidReason = reason;
        sale.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var voided = await LoadSaleAsync(id, tracked: false, cancellationToken);
        return PosMappings.ToSaleDto(voided);
    }

    public async Task<SaleRefundDto> RefundAsync(Guid id, RefundSaleInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        var reason = PosServiceHelpers.NormalizeOptional(input.Reason);
        if (reason is null)
        {
            throw new ValidationException(
                "The refund could not be recorded.",
                PosServiceHelpers.FieldError("reason", "A reason for the refund is required."));
        }

        var idempotencyKey = PosServiceHelpers.NormalizeOptional(input.IdempotencyKey);
        // The target sale is part of the operation's identity: without it, the same key
        // with the same tenders against a different sale would replay the wrong refund.
        var requestHash = idempotencyKey is null
            ? null
            : IdempotencyRequestHash.Compute($"{id:N}|{PosJson.Serialize(input)}");
        if (idempotencyKey is not null)
        {
            var (created, existing) = await _idempotency.TryAcquireAsync(
                idempotencyKey, RefundSaleOperation, actorUserId, requestHash, correlationId, cancellationToken);
            if (!created)
            {
                return ReplayStoredOrConflict<SaleRefundDto>(existing, "sale refund", requestHash);
            }
        }

        try
        {
            return await RefundSaleCoreAsync(id, input, reason, actorUserId, correlationId, idempotencyKey, cancellationToken);
        }
        catch
        {
            await MarkIdempotencyFailedAsync(idempotencyKey, RefundSaleOperation, actorUserId, "The refund failed and was rolled back.", cancellationToken);
            throw;
        }
    }

    private async Task<SaleRefundDto> RefundSaleCoreAsync(
        Guid id,
        RefundSaleInput input,
        string reason,
        string actorUserId,
        string correlationId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var sale = await LoadSaleAsync(id, tracked: true, cancellationToken);
        if (sale.Status != SaleStatus.Completed)
        {
            throw new ConflictException($"Sale '{sale.SaleNumber}' must be completed before it can be refunded.");
        }

        var originalTenders = sale.Payments.Where(p => !p.IsRefund).ToList();
        var refundPayments = input.Payments is { Count: > 0 } requested
            ? await ResolvePaymentsAsync(requested, cancellationToken)
            : originalTenders.Select(p => new ResolvedPayment(
                new SalePaymentInput(p.PaymentMethodCode, p.Amount, p.ExternalReference),
                new PaymentMethodEntity { Id = p.PaymentMethodId, Code = p.PaymentMethodCode, Name = p.PaymentMethodName, Kind = p.Kind })).ToList();

        var refundTotal = PosPricing.Money(refundPayments.Sum(p => p.Input.Amount));
        if (refundTotal != PosPricing.Money(sale.GrandTotal))
        {
            throw new ValidationException(
                "The refund could not be recorded.",
                PosServiceHelpers.FieldError("payments", $"Refund tenders total {refundTotal:F2} but the sale total is {sale.GrandTotal:F2}."));
        }

        foreach (var payment in refundPayments)
        {
            if (string.Equals(payment.Method.Kind, AccountKind, StringComparison.Ordinal) && sale.MemberId is null)
            {
                throw new ValidationException(
                    "The refund could not be recorded.",
                    PosServiceHelpers.FieldError("payments", "Account payments require a member."));
            }
        }

        var register = await LoadRegisterAsync(sale.RegisterId, cancellationToken);
        var session = await LoadOpenSessionAsync(register.Id, cancellationToken);

        foreach (var payment in refundPayments)
        {
            if (string.Equals(payment.Method.Kind, CashKind, StringComparison.Ordinal) && session is null)
            {
                throw new ValidationException(
                    "The refund could not be recorded.",
                    PosServiceHelpers.FieldError("payments", "Cash refunds require an open cashier session on the register."));
            }
        }

        var stockLines = sale.Lines.Where(l => l.StockManaged).ToList();
        var products = await LoadProductsAsync(stockLines.Select(l => l.ProductId).Distinct().ToList(), cancellationToken);

        var refundNumber = await _referenceGenerator.GenerateAsync(SaleRefundNumber.Prefix, SaleRefundNumber.Width, cancellationToken);
        var movementReferences = await AllocateManyAsync(StockMovementNumber.Prefix, stockLines.Count, cancellationToken);

        // Resolve the member account and allocate account-credit references before the
        // transaction, for the same reason as sale completion.
        Guid? refundAccountId = null;
        var creditReferences = new Dictionary<int, string>();
        for (var i = 0; i < refundPayments.Count; i++)
        {
            if (!string.Equals(refundPayments[i].Method.Kind, AccountKind, StringComparison.Ordinal))
            {
                continue;
            }

            refundAccountId ??= sale.MemberId is { } refundMemberId
                ? (await _accounts.GetAccountForMemberAsync(refundMemberId, cancellationToken)).Id
                : null;
            creditReferences[i] = await _referenceGenerator.GenerateAsync(CreditReferencePrefix, cancellationToken: cancellationToken);
        }

        var now = _clock.UtcNow;

        // D5.5: snapshot the tax of every refunded line, not just its price. A credit note
        // reverses specific lines, so it has to reverse their tax treatment, rate and
        // amount; a share of the invoice tax total cannot reproduce that on a mixed-rate
        // sale. Copied verbatim from the sale line, which is itself the historical
        // snapshot taken at sale time (P3:1482-1490).
        var refundLines = sale.Lines.Select(l => new SaleRefundLineDto(
            l.ProductId,
            l.ProductSku,
            l.ProductName,
            l.Quantity,
            l.UnitPrice,
            l.LineTotal,
            l.TaxableBase,
            l.TaxAmount,
            l.TaxTreatment.ToString(),
            l.TaxRateName,
            l.TaxRate)).ToList();

        var refundPaymentDtos = refundPayments.Select(p => new SaleRefundPaymentDto(
            p.Method.Code,
            p.Method.Name,
            p.Method.Kind,
            p.Input.Amount,
            PosServiceHelpers.NormalizeOptional(p.Input.ExternalReference))).ToList();

        var refund = new SaleRefundEntity
        {
            RefundNumber = refundNumber,
            SaleId = sale.Id,
            SaleNumber = sale.SaleNumber,
            Status = "Completed",
            CurrencyCode = sale.CurrencyCode,
            Amount = sale.GrandTotal,
            Reason = reason,
            RefundedByUserId = actorUserId,
            RefundedAtUtc = now,
            LinesJson = PosJson.Serialize(refundLines),
            PaymentsJson = PosJson.Serialize(refundPaymentDtos),
            CorrelationId = correlationId,
            CreatedAtUtc = now,
        };

        await _unitOfWork.BeginAsync(cancellationToken);
        try
        {
            if (stockLines.Count > 0)
            {
                var lockKeys = stockLines.Select(l => (l.ProductId, sale.LocationId)).ToList();
                await StockLedgerWriter.AcquireLocksAsync(_context, lockKeys, cancellationToken);
            }

            for (var i = 0; i < stockLines.Count; i++)
            {
                var line = stockLines[i];
                var product = products[line.ProductId];
                await _ledger.ApplyAsync(
                    StockMovementType.Return,
                    line.ProductId,
                    sale.LocationId,
                    line.Quantity,
                    product.Cost,
                    sale.CurrencyCode,
                    sale.BusinessDateUtc,
                    actorUserId,
                    reason,
                    PosMovementSources.SaleRefund,
                    refund.Id,
                    movementReferences[i],
                    cancellationToken);
            }

            for (var i = 0; i < refundPayments.Count; i++)
            {
                var payment = refundPayments[i];
                if (string.Equals(payment.Method.Kind, CashKind, StringComparison.Ordinal) && session is not null)
                {
                    _context.SalePayments.Add(new SalePaymentEntity
                    {
                        SaleId = sale.Id,
                        CashierSessionId = session.Id,
                        PaymentMethodId = payment.Method.Id,
                        PaymentMethodCode = payment.Method.Code,
                        PaymentMethodName = payment.Method.Name,
                        Kind = payment.Method.Kind,
                        Amount = payment.Input.Amount,
                        IsRefund = true,
                        ExternalReference = PosServiceHelpers.NormalizeOptional(payment.Input.ExternalReference),
                        CreatedAtUtc = now,
                    });
                }
                else if (string.Equals(payment.Method.Kind, AccountKind, StringComparison.Ordinal) && refundAccountId is { } accountId)
                {
                    await _accounts.CreateCreditAsync(accountId, new CreditInput(
                        $"Sale refund {refund.RefundNumber} for sale {sale.SaleNumber}",
                        payment.Input.Amount,
                        sale.CurrencyCode,
                        PosServiceHelpers.NormalizeOptional(payment.Input.ExternalReference),
                        sale.BusinessDateUtc,
                        Allocations: null,
                        creditReferences[i]), actorUserId, cancellationToken);
                }
            }

            _context.SaleRefunds.Add(refund);
            await _context.SaveChangesAsync(cancellationToken);

            await _outbox.EnqueueAsync(
                PosEventTypes.SaleRefundEvent,
                PosJson.Serialize(new SaleRefundEvent(
                    refund.Id,
                    refund.RefundNumber,
                    sale.Id,
                    sale.SaleNumber,
                    sale.PosOutletId,
                    sale.RegisterId,
                    sale.LocationId,
                    actorUserId,
                    sale.MemberId,
                    sale.CurrencyCode,
                    sale.GrandTotal,
                    refundPaymentDtos,
                    now,
                    correlationId)),
                correlationId,
                cancellationToken);

            // D5.5: a completed refund owes a fiscal credit note, recorded in the same
            // transaction and driven by the outbox worker afterwards.
            await _fiscalisation.RequireAsync(sale, refund, correlationId, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var dto = PosMappings.ToRefundDto(refund);
        if (idempotencyKey is not null)
        {
            await _idempotency.CompleteAsync(idempotencyKey, RefundSaleOperation, actorUserId, PosJson.Serialize(dto), cancellationToken);
        }

        return dto;
    }

    public async Task<SaleDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await LoadSaleAsync(id, tracked: false, cancellationToken);
        return PosMappings.ToSaleDto(sale);
    }

    public async Task<ReceiptDto> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await LoadSaleAsync(id, tracked: false, cancellationToken);
        var receipt = sale.Receipts.OrderByDescending(r => r.GeneratedAtUtc).FirstOrDefault()
                      ?? throw new NotFoundException($"Sale '{sale.SaleNumber}' has no receipt.");
        return PosMappings.ToReceiptDto(receipt);
    }

    public async Task<SaleSearchResult> SearchAsync(SaleSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PosServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<SaleEntity> query = _context.Sales.AsNoTracking();

        if (input.Status is { } statusText)
        {
            var status = PosServiceHelpers.ParseEnum<SaleStatus>(statusText, "status");
            query = query.Where(s => s.Status == status);
        }

        if (input.RegisterId is { } registerId)
        {
            query = query.Where(s => s.RegisterId == registerId);
        }

        if (input.MemberId is { } memberId)
        {
            query = query.Where(s => s.MemberId == memberId);
        }

        if (input.FromDateUtc is { } fromDate)
        {
            query = query.Where(s => s.BusinessDateUtc >= fromDate);
        }

        if (input.ToDateUtc is { } toDate)
        {
            query = query.Where(s => s.BusinessDateUtc < toDate.AddDays(1));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await LoadSaleQueryAsync(query)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new SaleSearchResult(items.Select(PosMappings.ToSaleDto).ToList(), total, page, pageSize);
    }

    private T ReplayStoredOrConflict<T>(StoredIdempotencyResult? existing, string operation, string? requestHash)
    {
        // A key reused with a materially different payload must never replay the
        // original operation's result, and must not be allowed to overwrite it.
        if (existing is not null
            && requestHash is not null
            && existing.RequestHash is not null
            && !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException(
                $"The idempotency key for this {operation} was already used with a different request payload.");
        }

        if (existing is { Status: IdempotencyStatus.Completed } && existing.ResultJson is not null)
        {
            return PosJson.Deserialize<T>(existing.ResultJson);
        }

        if (existing is { Status: IdempotencyStatus.InProgress })
        {
            throw new ConflictException($"The {operation} with this idempotency key is still in progress.");
        }

        if (existing is { Status: IdempotencyStatus.Failed })
        {
            throw new ConflictException($"The {operation} with this idempotency key previously failed; retry the request.");
        }

        throw new ConflictException($"The {operation} with this idempotency key could not be processed.");
    }

    /// <summary>
    /// Releases a claimed idempotency key after a failed attempt. A secondary failure
    /// here must never mask the original error the caller is about to rethrow.
    /// </summary>
    private async Task MarkIdempotencyFailedAsync(
        string? idempotencyKey,
        string operation,
        string actorUserId,
        string error,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null)
        {
            return;
        }

        try
        {
            await _idempotency.FailAsync(idempotencyKey, operation, actorUserId, error, cancellationToken);
        }
        catch
        {
        }
    }

    private sealed record ResolvedPayment(SalePaymentInput Input, PaymentMethodEntity Method);

    private async Task<List<ResolvedPayment>> ResolvePaymentsAsync(IReadOnlyList<SalePaymentInput> payments, CancellationToken cancellationToken)
    {
        var methods = await _context.PaymentMethods
            .Where(p => payments.Select(x => x.PaymentMethodCode.Trim().ToUpperInvariant()).Distinct().Contains(p.Code))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var result = new List<ResolvedPayment>(payments.Count);
        foreach (var payment in payments)
        {
            var code = payment.PaymentMethodCode.Trim().ToUpperInvariant();
            var method = methods.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.Ordinal))
                         ?? throw new NotFoundException($"Payment method '{payment.PaymentMethodCode}' was not found.");
            if (!method.IsActive)
            {
                throw new ConflictException($"Payment method '{method.Code}' is inactive.");
            }

            result.Add(new ResolvedPayment(payment, method));
        }

        return result;
    }

    private async Task<List<string>> AllocateManyAsync(string prefix, int count, CancellationToken cancellationToken)
    {
        var references = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            references.Add(await _referenceGenerator.GenerateAsync(prefix, StockMovementNumber.Width, cancellationToken));
        }

        return references;
    }

    private async Task<Dictionary<int, string>> AllocateChargeReferencesAsync(IReadOnlyList<ResolvedPayment> payments, CancellationToken cancellationToken)
    {
        var references = new Dictionary<int, string>();
        for (var i = 0; i < payments.Count; i++)
        {
            if (string.Equals(payments[i].Method.Kind, AccountKind, StringComparison.Ordinal))
            {
                references[i] = await _referenceGenerator.GenerateAsync(ChargeReferencePrefix, cancellationToken: cancellationToken);
            }
        }

        return references;
    }

    private async Task<(Guid? Id, string? Number, string? Name)> ResolveMemberAsync(Guid? memberId, string? credentialValue, CancellationToken cancellationToken)
    {
        var resolved = memberId;
        if (credentialValue is not null)
        {
            var result = await _credentials.ResolveAsync(credentialValue, cancellationToken);
            if (memberId is { } requested && requested != result.MemberId)
            {
                throw new ConflictException("The credential belongs to a different member.");
            }

            resolved = result.MemberId;
        }

        if (resolved is null)
        {
            return (null, null, null);
        }

        var member = await _context.Members
            .Include(m => m.Person)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == resolved, cancellationToken)
            ?? throw new NotFoundException($"Member '{resolved}' was not found.");

        if (member.Status != MemberStatus.Active)
        {
            throw new ConflictException($"Member '{member.MemberNumber}' is not active.");
        }

        return (member.Id, member.MemberNumber, PosServiceHelpers.MemberDisplayName(member));
    }

    private async Task<string> ResolveCurrencyCodeAsync(CancellationToken cancellationToken)
    {
        return await _context.Currencies
                   .Where(c => c.IsDefault)
                   .Select(c => c.Code)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new ConflictException("No default currency is configured for the club.");
    }

    private async Task EnsureStockAvailableAsync(
        IReadOnlyList<CreateSaleLineInput> lines,
        IReadOnlyDictionary<Guid, ProductEntity> products,
        Guid locationId,
        CancellationToken cancellationToken)
    {
        var managedIds = lines.Where(l => products[l.ProductId].StockManaged).Select(l => l.ProductId).Distinct().ToList();
        if (managedIds.Count == 0)
        {
            return;
        }

        var balances = await _context.StockBalances
            .Where(b => b.LocationId == locationId && managedIds.Contains(b.ProductId))
            .ToDictionaryAsync(b => b.ProductId, b => b.Quantity, cancellationToken);

        foreach (var line in lines)
        {
            if (!products[line.ProductId].StockManaged)
            {
                continue;
            }

            var available = balances.GetValueOrDefault(line.ProductId);
            if (line.Quantity > available)
            {
                throw new ConflictException($"Product '{products[line.ProductId].Sku}' only has {available:F0} available but {line.Quantity:F0} were requested.");
            }
        }
    }

    private async Task<Dictionary<Guid, ProductEntity>> LoadProductsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, ProductEntity>();
        }

        var products = await _context.Products
            .Include(p => p.TaxRate)
            .Where(p => distinctIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = distinctIds.FirstOrDefault(id => !products.ContainsKey(id));
        if (missing != Guid.Empty)
        {
            throw new NotFoundException($"Product '{missing}' was not found.");
        }

        return products;
    }

    private async Task<PosRegisterEntity> LoadRegisterAsync(Guid id, CancellationToken cancellationToken)
    {
        var register = await _context.PosRegisters
            .Include(r => r.PosOutlet)
            .Include(r => r.InventoryLocation)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException($"POS register '{id}' was not found.");

        if (register.Status != PosRegisterStatus.Active)
        {
            throw new ConflictException($"POS register '{register.Code}' is inactive.");
        }

        return register;
    }

    private async Task<CashierSessionEntity> LoadOpenSessionAsync(Guid registerId, CancellationToken cancellationToken)
    {
        var session = await _context.CashierSessions
            .Where(s => s.RegisterId == registerId
                        && (s.Status == CashierSessionStatus.Open || s.Status == CashierSessionStatus.Active))
            .OrderByDescending(s => s.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("No open cashier session is available on this register; open one before taking or refunding sales.");

        return session;
    }

    private async Task<SaleEntity> LoadSaleAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = LoadSaleQueryAsync(_context.Sales);
        var resultQuery = tracked ? query : query.AsNoTracking();
        return await resultQuery.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Sale '{id}' was not found.");
    }

    private static IQueryable<SaleEntity> LoadSaleQueryAsync(IQueryable<SaleEntity> query)
    {
        return query
            .Include(s => s.Register)
                .ThenInclude(r => r.PosOutlet)
            .Include(s => s.Register)
                .ThenInclude(r => r.InventoryLocation)
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.Discounts)
            .Include(s => s.Receipts);
    }
}