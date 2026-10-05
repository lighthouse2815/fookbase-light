namespace Fookbase.Api.Modules.Admin.DTOs.Requests;

public sealed record DismissReportRequest(string? Reason, string? InternalNote);
