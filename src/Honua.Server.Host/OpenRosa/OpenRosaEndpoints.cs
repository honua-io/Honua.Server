// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.OpenRosa;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.OpenRosa;

public static class OpenRosaEndpoints
{
    public static RouteGroupBuilder MapOpenRosa(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/openrosa")
            .WithTags("OpenRosa")
            .RequireAuthorization();

        // FormList endpoint - Returns list of available forms
        group.MapGet("/formList", async (
            [FromServices] IMetadataRegistry metadata,
            [FromServices] IXFormGenerator xformGenerator,
            HttpContext context) =>
        {
            var snapshot = await metadata.GetSnapshotAsync();
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

            var formsXml = new XElement("xforms",
                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),

                from service in snapshot.Services
                from layer in service.Layers
                where layer.OpenRosa?.Enabled == true
                let openrosa = layer.OpenRosa!
                let formId = openrosa.FormId ?? $"{service.Id}_{layer.Id}"
                select new XElement("xform",
                    new XElement("formID", formId),
                    new XElement("name", openrosa.FormTitle ?? layer.Title),
                    new XElement("version", openrosa.FormVersion),
                    new XElement("downloadUrl", $"{baseUrl}/openrosa/forms/{formId}"),
                    new XElement("manifestUrl", $"{baseUrl}/openrosa/forms/{formId}/manifest")
                )
            );

            var doc = new XDocument(formsXml);
            return Results.Content(doc.ToString(), "text/xml; charset=utf-8");
        })
        .WithName("OpenRosaFormList")
        .WithSummary("Get list of available OpenRosa forms")
        .Produces(200, contentType: "text/xml");

        // Forms endpoint - Returns XForm XML for a specific form
        group.MapGet("/forms/{formId}", async (
            string formId,
            [FromServices] IMetadataRegistry metadata,
            [FromServices] IXFormGenerator xformGenerator,
            HttpContext context) =>
        {
            var snapshot = await metadata.GetSnapshotAsync();
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";

            // Find layer by formId
            LayerDefinition? targetLayer = null;
            foreach (var service in snapshot.Services)
            {
                foreach (var layer in service.Layers)
                {
                    if (layer.OpenRosa?.Enabled == true)
                    {
                        var layerFormId = layer.OpenRosa.FormId ?? $"{service.Id}_{layer.Id}";
                        if (string.Equals(layerFormId, formId, StringComparison.OrdinalIgnoreCase))
                        {
                            targetLayer = layer;
                            break;
                        }
                    }
                }
                if (targetLayer != null) break;
            }

            if (targetLayer is null)
            {
                return GeoservicesRESTErrorHelper.NotFound("Form", formId);
            }

            var xform = xformGenerator.Generate(targetLayer, baseUrl);
            return Results.Content(xform.Xml.ToString(), "text/xml; charset=utf-8");
        })
        .WithName("OpenRosaGetForm")
        .WithSummary("Download XForm XML for a specific form")
        .Produces(200, contentType: "text/xml")
        .Produces(404);

        // Submission endpoint - HEAD (discovery)
        group.MapMethods("/submission", new[] { "HEAD" }, () => Results.Ok())
            .WithName("OpenRosaSubmissionHead")
            .WithSummary("Check submission endpoint availability")
            .Produces(200);

        // Submission endpoint - POST (accept submissions)
        group.MapPost("/submission", async (
            HttpRequest request,
            [FromServices] ISubmissionProcessor processor,
            [FromServices] IOptions<OpenRosaOptions> openRosaOptions,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var username = context.User.Identity?.Name ?? "anonymous";

            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = "Expected multipart/form-data" });
            }

            try
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var options = openRosaOptions.Value;
                var maxSubmissionBytes = Math.Max(1, options.MaxSubmissionSizeMB) * 1024L * 1024L;
                var totalBytes = form.Files.Sum(f => f.Length);
                if (totalBytes > maxSubmissionBytes)
                {
                    return Results.Problem(
                        detail: $"Submission exceeds maximum size of {options.MaxSubmissionSizeMB} MB.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                var allowedMediaTypes = options.AllowedMediaTypes?.Count > 0
                    ? new HashSet<string>(options.AllowedMediaTypes, StringComparer.OrdinalIgnoreCase)
                    : null;

                // Extract XML submission file
                var xmlFile = form.Files.GetFile("xml_submission_file");
                if (xmlFile is null)
                {
                    return Results.BadRequest(new { error = "Missing xml_submission_file" });
                }

                XDocument xmlDoc;
                await using (var xmlStream = xmlFile.OpenReadStream())
                {
                    // Validate stream size before processing to prevent DoS
                    SecureXmlSettings.ValidateStreamSize(xmlStream);

                    // Use secure XML parsing to prevent XXE attacks
                    xmlDoc = await SecureXmlSettings.LoadSecureAsync(xmlStream, LoadOptions.None, cancellationToken);
                }

                // Extract attachments without buffering entire files in memory
                var attachments = new List<AttachmentFile>();
                foreach (var file in form.Files.Where(f => !string.Equals(f.Name, "xml_submission_file", StringComparison.Ordinal)))
                {
                    var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
                    if (allowedMediaTypes is not null && !allowedMediaTypes.Contains(contentType))
                    {
                        return Results.BadRequest(new { error = $"Attachment '{file.FileName}' uses unsupported media type '{contentType}'." });
                    }

                    attachments.Add(new AttachmentFile
                    {
                        Filename = file.FileName,
                        ContentType = contentType,
                        SizeBytes = file.Length,
                        OpenStreamAsync = ct =>
                        {
                            ct.ThrowIfCancellationRequested();
                            var stream = file.OpenReadStream();
                            return Task.FromResult<Stream>(stream);
                        }
                    });
                }

                var submissionRequest = new SubmissionRequest
                {
                    XmlDocument = xmlDoc,
                    SubmittedBy = username,
                    DeviceId = request.Headers["X-OpenRosa-DeviceID"].FirstOrDefault(),
                    Attachments = attachments
                };

                var result = await processor.ProcessAsync(submissionRequest, cancellationToken);

                if (result.Success)
                {
                    // OpenRosa 201 Created response
                    var responseXml = new XDocument(
                        new XElement("OpenRosaResponse",
                            new XAttribute(XNamespace.Xmlns + "orx", "http://openrosa.org/http/response"),
                            new XElement("message", result.ResultType switch
                            {
                                SubmissionResultType.DirectPublished => "Submission accepted and published",
                                SubmissionResultType.StagedForReview => "Submission accepted for review",
                                _ => "Submission accepted"
                            }),
                            new XElement("submissionMetadata",
                                new XAttribute("xmlns", "http://openrosa.org/xforms/metadata"),
                                new XElement("instanceID", result.InstanceId)
                            )
                        )
                    );

                    return Results.Content(responseXml.ToString(), "text/xml; charset=utf-8", statusCode: 201);
                }
                else
                {
                    var errorXml = new XDocument(
                        new XElement("OpenRosaResponse",
                            new XElement("message", result.ErrorMessage ?? "Submission rejected")
                        )
                    );

                    return Results.Content(errorXml.ToString(), "text/xml; charset=utf-8", statusCode: 400);
                }
            }
            catch (Exception ex)
            {
                var errorXml = new XDocument(
                    new XElement("OpenRosaResponse",
                        new XElement("message", $"Server error: {ex.Message}")
                    )
                );

                return Results.Content(errorXml.ToString(), "text/xml; charset=utf-8", statusCode: 500);
            }
        })
        .WithName("OpenRosaSubmission")
        .WithSummary("Submit filled form with attachments")
        .Accepts<IFormFileCollection>("multipart/form-data")
        .Produces(201, contentType: "text/xml")
        .Produces(400, contentType: "text/xml")
        .Produces(500, contentType: "text/xml")
        .DisableAntiforgery();

        // Manifest endpoint (placeholder for future attachment management)
        group.MapGet("/forms/{formId}/manifest", (string formId) =>
        {
            // Empty manifest - no pre-loaded media files
            var manifestXml = new XDocument(
                new XElement("manifest",
                    new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema")
                )
            );

            return Results.Content(manifestXml.ToString(), "text/xml; charset=utf-8");
        })
        .WithName("OpenRosaManifest")
        .WithSummary("Get form manifest (media files)")
        .Produces(200, contentType: "text/xml");

        return group;
    }
}
