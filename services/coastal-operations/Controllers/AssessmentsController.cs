using System.Diagnostics;
using Blueverse.CoastalOperations.Application;
using Blueverse.CoastalOperations.Contracts;
using Blueverse.CoastalOperations.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.CoastalOperations.Controllers;

[ApiController]
[Authorize]
[Route("api/operations/assessments")]
public sealed class AssessmentsController(AssessmentApplicationService assessments) : ControllerBase
{
    [HttpPost]
    [HasPermission(PermissionCodes.OperationsAssessmentCreate)]
    [ProducesResponseType(typeof(AssessmentResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await assessments.CreateAsync(
            request,
            AuthenticatedActor.GetId(User),
            CorrelationId(),
            Request.Headers["Idempotency-Key"].ToString(),
            cancellationToken);
        return StoredResponse(result);
    }

    [HttpGet]
    [HasPermission(PermissionCodes.OperationsAssessmentRead)]
    [ProducesResponseType(typeof(AssessmentQueueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentQueueResponse>> List(
        [FromQuery] AssessmentListQuery query,
        CancellationToken cancellationToken)
    {
        var actorId = AuthenticatedActor.GetId(User);
        var canReadQueue = User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        return Ok(await assessments.GetQueueAsync(query, actorId, canReadQueue, cancellationToken));
    }

    [HttpGet("{assessmentId:guid}")]
    [HasPermission(PermissionCodes.OperationsAssessmentRead)]
    [ProducesResponseType(typeof(AssessmentDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentDetailResponse>> Get(
        Guid assessmentId,
        CancellationToken cancellationToken)
    {
        var actorId = AuthenticatedActor.GetId(User);
        var canReadQueue = User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        var canReadEvidence = User.HasClaim("permission", CoastalPermissions.EvidenceRead);
        return Ok(await assessments.GetDetailAsync(assessmentId, actorId, canReadQueue, canReadEvidence, cancellationToken));
    }

    [HttpPost("{assessmentId:guid}/decisions")]
    [HasPermission(PermissionCodes.OperationsAssessmentDecide)]
    [ProducesResponseType(typeof(ReviewerDecisionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decide(
        Guid assessmentId,
        [FromBody] ReviewerDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await assessments.DecideAsync(
            assessmentId,
            request,
            AuthenticatedActor.GetId(User),
            CorrelationId(),
            Request.Headers["Idempotency-Key"].ToString(),
            cancellationToken);
        return StoredResponse(result);
    }

    [HttpPost("{assessmentId:guid}/evidence")]
    [HasPermission(PermissionCodes.OperationsEvidenceUpload)]
    [RequestSizeLimit(5 * 1024 * 1024 + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024 + 64 * 1024)]
    [ProducesResponseType(typeof(AssessmentEvidenceUploadResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AssessmentEvidenceUploadResponse>> UploadEvidence(
        Guid assessmentId,
        [FromForm] AssessmentEvidenceUploadRequest request,
        [FromServices] AssessmentEvidenceApplicationService evidence,
        CancellationToken cancellationToken)
    {
        if (request.Image is null)
            return BadRequest(new ProblemDetails { Title = "Image is required", Status = StatusCodes.Status400BadRequest });

        await using var stream = request.Image.OpenReadStream();
        var result = await evidence.UploadAsync(
            assessmentId, AuthenticatedActor.GetId(User), CorrelationId(),
            request.Image.ContentType, stream, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{assessmentId:guid}/evidence/{evidenceId:guid}")]
    [HasPermission(PermissionCodes.OperationsEvidenceRead)]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvidence(
        Guid assessmentId,
        Guid evidenceId,
        [FromServices] AssessmentEvidenceApplicationService evidence,
        CancellationToken cancellationToken)
    {
        var canReadQueue = User.HasClaim("permission", CoastalPermissions.AssessmentQueueRead);
        var result = await evidence.GetContentAsync(
            assessmentId, evidenceId, AuthenticatedActor.GetId(User), canReadQueue, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(result.Bytes, result.MediaType);
    }

    private string CorrelationId() => User.FindFirst("correlation_id")?.Value ??
        Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;

    private IActionResult StoredResponse<T>(StoredOutcome<T> outcome) =>
        new ContentResult
        {
            StatusCode = outcome.StatusCode,
            ContentType = "application/json; charset=utf-8",
            Content = outcome.SerializedBody
        };
}
