using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.Certificates;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

/// <summary>
/// Manages the local client-certificate store and its Azure App Registration uploads.
/// Private key material is never returned; PFX export requires a password and is audit-logged.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class CertificatesController(
    ICertificateService certificateService,
    ICertificateUploadService uploadService) : ControllerBase
{
    // ── Listing ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var certificates = await certificateService.ListAsync(cancellationToken);

        // Merge in used-by info so the list view can show reference counts without N+1 calls.
        var withUsage = new List<object>(certificates.Count);
        foreach (var cert in certificates)
        {
            var usage = await certificateService.GetUsageAsync(cert.Name, cancellationToken);
            withUsage.Add(new { certificate = cert, usedByConnections = usage.ConnectionNames, usedByHttpApis = usage.HttpApiNames });
        }

        return Ok(withUsage);
    }

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int days = 30, CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 365) return BadRequest(new { error = "days must be between 1 and 365." });
        return Ok(await certificateService.GetExpiringAsync(days, cancellationToken));
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] string? name, [FromQuery] int limit = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await certificateService.ReadAuditAsync(name, limit, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name, CancellationToken cancellationToken)
    {
        try
        {
            var certificate = await certificateService.GetAsync(name, cancellationToken);
            if (certificate is null) return NotFound();
            var usage = await certificateService.GetUsageAsync(name, cancellationToken);
            return Ok(new { certificate, usedByConnections = usage.ConnectionNames, usedByHttpApis = usage.HttpApiNames });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Generation / deletion ────────────────────────────────────────────────

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateCertificateApiRequest request, CancellationToken cancellationToken)
    {
        var result = await certificateService.GenerateSelfSignedAsync(new GenerateCertificateRequest
        {
            Name = request.Name?.Trim() ?? string.Empty,
            SubjectCn = request.SubjectCn,
            KeySize = request.KeySize,
            ValidityMonths = request.ValidityMonths,
            PfxPassword = request.PfxPassword,
        }, cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken cancellationToken)
    {
        try
        {
            await certificateService.DeleteAsync(name, cancellationToken);
            return NoContent();
        }
        catch (CertificateInUseException ex)
        {
            return Conflict(new
            {
                error = ex.Message,
                usedByConnections = ex.Usage.ConnectionNames,
                usedByHttpApis = ex.Usage.HttpApiNames,
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    // ── App Registration upload ──────────────────────────────────────────────

    [HttpPost("{name}/upload")]
    public async Task<IActionResult> Upload(string name, [FromBody] UploadCertificateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId))
            return BadRequest(new { error = "appId is required." });

        try
        {
            return Ok(await uploadService.UploadAsync(name, request.AppId.Trim(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{name}/upload-status")]
    public async Task<IActionResult> UploadStatus(string name, [FromQuery] string appId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return BadRequest(new { error = "appId is required." });

        try
        {
            var status = await uploadService.VerifyUploadAsync(name, appId.Trim(), cancellationToken);
            return Ok(new { status = status.ToString() });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("key-credentials")]
    public async Task<IActionResult> ListKeyCredentials([FromQuery] string appId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return BadRequest(new { error = "appId is required." });

        try
        {
            return Ok(await uploadService.ListKeyCredentialsAsync(appId.Trim(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("key-credentials")]
    public async Task<IActionResult> RemoveKeyCredential([FromBody] RemoveKeyCredentialRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppId) || string.IsNullOrWhiteSpace(request.KeyId))
            return BadRequest(new { error = "appId and keyId are required." });

        try
        {
            await uploadService.RemoveKeyCredentialAsync(request.AppId.Trim(), request.KeyId.Trim(), cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{name}/test-token")]
    public async Task<IActionResult> TestToken(string name, [FromBody] TestTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) || string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.Scope))
            return BadRequest(new { error = "tenantId, clientId, and scope are required." });

        return Ok(await uploadService.TestTokenAsync(name, request.TenantId, request.ClientId, request.Scope, cancellationToken));
    }

    // ── Downloads ────────────────────────────────────────────────────────────

    /// <summary>PEM download returns the public certificate only — never key material.</summary>
    [HttpGet("{name}/download")]
    public async Task<IActionResult> DownloadPem(string name, CancellationToken cancellationToken)
    {
        try
        {
            var pem = await certificateService.GetPublicCertPemAsync(name, cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(pem), "application/x-pem-file", $"{name}.pem");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>PFX export requires a password (POST body so it never appears in a URL).</summary>
    [HttpPost("{name}/export-pfx")]
    public async Task<IActionResult> ExportPfx(string name, [FromBody] ExportPfxRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required for PFX export." });

        try
        {
            var bytes = await certificateService.ExportPfxAsync(name, request.Password, cancellationToken);
            return File(bytes, "application/x-pkcs12", $"{name}.pfx");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    // ── CSR flow ─────────────────────────────────────────────────────────────

    [HttpPost("csr")]
    public async Task<IActionResult> CreateCsr([FromBody] CreateCsrApiRequest request, CancellationToken cancellationToken)
    {
        var result = await certificateService.CreateCsrAsync(new CreateCsrRequest
        {
            Name = request.Name?.Trim() ?? string.Empty,
            SubjectCn = request.SubjectCn,
            KeySize = request.KeySize,
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{name}/csr")]
    public async Task<IActionResult> DownloadCsr(string name, CancellationToken cancellationToken)
    {
        try
        {
            var pem = await certificateService.GetCsrPemAsync(name, cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(pem), "application/x-pem-file", $"{name}.csr");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{name}/import-issued")]
    public async Task<IActionResult> ImportIssued(string name, [FromBody] ImportIssuedCertificateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CertificatePem))
            return BadRequest(new { error = "certificatePem is required." });

        try
        {
            return Ok(await certificateService.ImportIssuedCertificateAsync(name, request.CertificatePem, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
