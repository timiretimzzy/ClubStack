using System.Text;
using ClubStack.Api.Contracts;
using ClubStack.Application.Auditing;
using ClubStack.Application.Authorization;
using Asp.Versioning;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubStack.Api.Controllers;

/// <summary>Audit activity query and export (D1 Identity/Platform, Phase 8 §8.2 audit).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiConstants.ApiRoutePrefix + "/audit")]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditRecorder _auditRecorder;

    public AuditController(IAuditRecorder auditRecorder)
    {
        _auditRecorder = auditRecorder;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:" + Permissions.AuditView)]
    [ProducesResponseType(typeof(AuditQueryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query(
        [FromQuery] string? action,
        [FromQuery] string? actorUserId,
        [FromQuery] string? actorName,
        [FromQuery] string? entityType,
        [FromQuery] string? correlationId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditRecorder.QueryAsync(
            new AuditQueryInput(action, actorUserId, actorName, entityType, correlationId, from, to, page, pageSize),
            cancellationToken);

        return Ok(new AuditQueryResponse
        {
            Items = result.Items.Select(ToResponse).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        });
    }

    [HttpGet("export")]
    [Authorize(Policy = "Permission:" + Permissions.AuditExport)]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string? action,
        [FromQuery] string? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditRecorder.QueryAsync(
            new AuditQueryInput(action, actorUserId, null, null, null, from, to, 1, Math.Clamp(pageSize, 1, 2000)),
            cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("id,timestamp_utc,actor_user_id,actor_name,action,entity_type,entity_id,reference,result,correlation_id");

        foreach (var entry in result.Items)
        {
            csv.AppendLine(string.Join(",",
                Csv(entry.Id.ToString()),
                Csv(entry.TimestampUtc.ToString("O")),
                Csv(entry.ActorUserId),
                Csv(entry.ActorName),
                Csv(entry.Action),
                Csv(entry.EntityType),
                Csv(entry.EntityId),
                Csv(entry.Reference),
                Csv(entry.Result),
                Csv(entry.CorrelationId)));
        }

        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray(),
            "text/csv",
            $"clubstack-audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static AuditEntryResponse ToResponse(AuditRecord record) => new()
    {
        Id = record.Id,
        TimestampUtc = record.TimestampUtc,
        ActorUserId = record.ActorUserId,
        ActorName = record.ActorName,
        Action = record.Action,
        EntityType = record.EntityType,
        EntityId = record.EntityId,
        Reference = record.Reference,
        Result = record.Result,
        CorrelationId = record.CorrelationId,
        MetadataJson = record.MetadataJson,
    };

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}