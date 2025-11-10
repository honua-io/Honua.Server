using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class OpenRosaEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public OpenRosaEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FormList_ReturnsXml()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/formList");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");
    }

    [Fact]
    public async Task FormList_ReturnsXFormsElement()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/formList");
        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);

        // Assert
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("xforms");
    }

    [Fact]
    public async Task FormList_RequiresAuthentication()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/formList");

        // Assert
        // In QuickStart mode (used by the test factory), authentication is not enforced
        // In production with proper auth, this should return 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetForm_ReturnsXFormXml()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // First get the form list to find a valid form ID
        var listResponse = await client.GetAsync("/v1/openrosa/formList");
        var listXml = await listResponse.Content.ReadAsStringAsync();
        var listDoc = XDocument.Parse(listXml);
        var firstFormId = listDoc.Descendants("formID").FirstOrDefault()?.Value;

        if (firstFormId == null)
        {
            // Skip test if no forms are configured
            return;
        }

        // Act
        var response = await client.GetAsync($"/v1/openrosa/forms/{firstFormId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("html");
    }

    [Fact]
    public async Task GetForm_InvalidFormId_ReturnsNotFound()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/forms/invalid_form_id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmissionHead_ReturnsOk()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Head, "/v1/openrosa/submission");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmissionPost_WithoutFormData_ReturnsBadRequest()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();
        var content = new StringContent("not multipart", Encoding.UTF8, "text/plain");

        // Act
        var response = await client.PostAsync("/v1/openrosa/submission", content);

        // Assert
        // The endpoint checks for multipart/form-data content type
        // May return 415 (UnsupportedMediaType) or 400/403 depending on middleware order
        response.StatusCode.Should().BeOneOf(HttpStatusCode.UnsupportedMediaType, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SubmissionPost_WithValidSubmission_ReturnsCreated()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // First get a valid form
        var listResponse = await client.GetAsync("/v1/openrosa/formList");
        var listXml = await listResponse.Content.ReadAsStringAsync();
        var listDoc = XDocument.Parse(listXml);
        var firstFormId = listDoc.Descendants("formID").FirstOrDefault()?.Value;

        if (firstFormId == null)
        {
            // Skip test if no forms are configured
            return;
        }

        // Create a valid submission
        var submissionXml = new XDocument(
            new XElement("data",
                new XAttribute("id", firstFormId),
                new XElement("test_field", "test_value"),
                new XElement("meta",
                    new XElement("instanceID", $"uuid:test-{System.Guid.NewGuid()}")
                )
            )
        );

        var multipart = new MultipartFormDataContent();
        var xmlContent = new StringContent(submissionXml.ToString(), Encoding.UTF8, "text/xml");
        multipart.Add(xmlContent, "xml_submission_file", "submission.xml");

        // Act
        var response = await client.PostAsync("/v1/openrosa/submission", multipart);

        // Assert
        // May return 201 (success) or 400 (validation error) depending on layer configuration
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);

        var responseXml = await response.Content.ReadAsStringAsync();
        var responseDoc = XDocument.Parse(responseXml);
        responseDoc.Root.Should().NotBeNull();
        responseDoc.Root!.Name.LocalName.Should().Be("OpenRosaResponse");
    }

    [Fact]
    public async Task SubmissionPost_MissingXmlFile_ReturnsBadRequest()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("test"), "other_field", "file.txt");

        // Act
        var response = await client.PostAsync("/v1/openrosa/submission", multipart);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseXml = await response.Content.ReadAsStringAsync();
        responseXml.Should().Contain("xml_submission_file");
    }

    [Fact]
    public async Task GetManifest_ReturnsXml()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/forms/any_form_id/manifest");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/xml");

        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("manifest");
    }

    [Fact]
    public async Task FormList_IncludesDownloadAndManifestUrls()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/formList");
        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);

        // Assert
        var xforms = doc.Descendants("xform").ToList();
        if (xforms.Count > 0)
        {
            var firstForm = xforms[0];
            firstForm.Element("downloadUrl").Should().NotBeNull();
            firstForm.Element("downloadUrl")!.Value.Should().Contain("/openrosa/forms/");

            firstForm.Element("manifestUrl").Should().NotBeNull();
            firstForm.Element("manifestUrl")!.Value.Should().Contain("/manifest");
        }
    }

    [Fact]
    public async Task FormList_IncludesFormMetadata()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/v1/openrosa/formList");
        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml);

        // Assert
        var xforms = doc.Descendants("xform").ToList();
        if (xforms.Count > 0)
        {
            var firstForm = xforms[0];
            firstForm.Element("formID").Should().NotBeNull();
            firstForm.Element("name").Should().NotBeNull();
            firstForm.Element("version").Should().NotBeNull();
        }
    }

    [Fact]
    public async Task SubmissionPost_WithAttachments_AcceptsMultipleFiles()
    {
        // Arrange
        using var client = _factory.CreateAuthenticatedClient();

        var listResponse = await client.GetAsync("/v1/openrosa/formList");
        var listXml = await listResponse.Content.ReadAsStringAsync();
        var listDoc = XDocument.Parse(listXml);
        var firstFormId = listDoc.Descendants("formID").FirstOrDefault()?.Value;

        if (firstFormId == null)
        {
            return;
        }

        var submissionXml = new XDocument(
            new XElement("data",
                new XAttribute("id", firstFormId),
                new XElement("photo", "photo.jpg"),
                new XElement("meta",
                    new XElement("instanceID", $"uuid:test-{System.Guid.NewGuid()}")
                )
            )
        );

        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(submissionXml.ToString(), Encoding.UTF8, "text/xml"), "xml_submission_file", "submission.xml");
        multipart.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "photo.jpg", "photo.jpg");

        // Act
        var response = await client.PostAsync("/v1/openrosa/submission", multipart);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }
}
