// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Abstract base class for building OGC capabilities documents.
/// Implements the Template Method pattern to consolidate common XML generation
/// across WMS, WFS, WMTS, WCS, and CSW protocols.
/// </summary>
public abstract class OgcCapabilitiesBuilder
{
    // Common OGC namespaces
    protected static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";
    protected static readonly XNamespace OwsV2 = "http://www.opengis.net/ows/2.0";
    protected static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    protected static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Template method that orchestrates the capabilities document generation asynchronously.
    /// This method is sealed to enforce the structure across all implementations.
    /// </summary>
    /// <param name="metadata">Metadata snapshot containing catalog information.</param>
    /// <param name="request">HTTP request for building endpoint URLs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>XML string containing the complete capabilities document.</returns>
    public async Task<string> BuildCapabilitiesAsync(MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(metadata);
        Guard.NotNull(request);

        var root = await BuildRootElementAsync(metadata, request, cancellationToken).ConfigureAwait(false);

        var doc = new XDocument(
            new XDeclaration("1.0", GetEncoding(), null),
            root);

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Builds the root element of the capabilities document asynchronously.
    /// </summary>
    protected virtual async Task<XElement> BuildRootElementAsync(MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken)
    {
        var root = new XElement(GetRootElementName(),
            GetNamespaceAttributes(),
            BuildServiceIdentification(metadata),
            BuildServiceProvider(metadata, request),
            BuildOperationsMetadata(request));

        await AddProtocolSpecificSectionsAsync(root, metadata, request, cancellationToken).ConfigureAwait(false);

        return root;
    }

    /// <summary>
    /// Gets the name of the root XML element (e.g., "Capabilities", "WMS_Capabilities").
    /// </summary>
    protected abstract XName GetRootElementName();

    /// <summary>
    /// Gets the OGC service name (e.g., "WMS", "WFS", "WMTS").
    /// </summary>
    protected abstract string GetServiceName();

    /// <summary>
    /// Gets the protocol version (e.g., "1.3.0", "2.0.0").
    /// </summary>
    protected abstract string GetVersion();

    /// <summary>
    /// Gets the XML namespace attributes for the root element.
    /// Subclasses should override to add protocol-specific namespaces.
    /// </summary>
    protected virtual IEnumerable<XAttribute> GetNamespaceAttributes()
    {
        yield return new XAttribute("version", GetVersion());
        yield return new XAttribute(XNamespace.Xmlns + "xsi", Xsi);
        yield return new XAttribute(XNamespace.Xmlns + "xlink", XLink);
    }

    /// <summary>
    /// Builds the ServiceIdentification section (OWS Common).
    /// </summary>
    protected virtual XElement BuildServiceIdentification(MetadataSnapshot metadata)
    {
        var serviceName = GetServiceName();
        var ns = GetOwsNamespace();

        var element = new XElement(ns + "ServiceIdentification",
            new XElement(ns + "Title", GetServiceTitle(metadata)),
            new XElement(ns + "Abstract", GetServiceAbstract(metadata)),
            new XElement(ns + "ServiceType", serviceName),
            new XElement(ns + "ServiceTypeVersion", GetVersion()));

        AddServiceIdentificationExtensions(element, metadata);

        element.Add(
            new XElement(ns + "Fees", "none"),
            new XElement(ns + "AccessConstraints", "none"));

        return element;
    }

    /// <summary>
    /// Builds the ServiceProvider section (OWS Common).
    /// </summary>
    protected virtual XElement BuildServiceProvider(MetadataSnapshot metadata, HttpRequest request)
    {
        var ns = GetOwsNamespace();
        var baseUrl = BuildBaseUrl(request);
        var contact = metadata.Catalog.Contact;

        var element = new XElement(ns + "ServiceProvider",
            new XElement(ns + "ProviderName", contact?.Organization ?? metadata.Catalog.Title));

        if (baseUrl.HasValue())
        {
            element.Add(new XElement(ns + "ProviderSite", new XAttribute(XLink + "href", baseUrl)));
        }

        if (contact != null)
        {
            element.Add(BuildContactInformation(contact, ns));
        }

        return element;
    }

    /// <summary>
    /// Builds the contact information element.
    /// </summary>
    protected virtual XElement BuildContactInformation(CatalogContactDefinition contact, XNamespace ns)
    {
        var serviceContact = new XElement(ns + "ServiceContact");

        if (contact.Name.HasValue())
        {
            serviceContact.Add(new XElement(ns + "IndividualName", contact.Name));
        }

        if (contact.Organization.HasValue())
        {
            serviceContact.Add(new XElement(ns + "PositionName", contact.Organization));
        }

        if (contact.Email.HasValue() || contact.Phone.HasValue())
        {
            var contactInfo = new XElement(ns + "ContactInfo");

            if (contact.Phone.HasValue())
            {
                contactInfo.Add(new XElement(ns + "Phone",
                    new XElement(ns + "Voice", contact.Phone)));
            }

            if (contact.Email.HasValue())
            {
                contactInfo.Add(new XElement(ns + "Address",
                    new XElement(ns + "ElectronicMailAddress", contact.Email)));
            }

            serviceContact.Add(contactInfo);
        }

        return serviceContact;
    }

    /// <summary>
    /// Builds the OperationsMetadata section (OWS Common).
    /// </summary>
    protected virtual XElement BuildOperationsMetadata(HttpRequest request)
    {
        var ns = GetOwsNamespace();
        var endpoint = BuildEndpointUrl(request);

        var element = new XElement(ns + "OperationsMetadata");

        foreach (var operation in GetSupportedOperations())
        {
            element.Add(BuildOperationElement(operation, endpoint, ns));
        }

        AddOperationsMetadataExtensions(element, ns);

        return element;
    }

    /// <summary>
    /// Builds an operation element with DCP/HTTP GET and optionally POST.
    /// </summary>
    protected virtual XElement BuildOperationElement(string operationName, string endpoint, XNamespace ns)
    {
        var operation = new XElement(ns + "Operation",
            new XAttribute("name", operationName),
            new XElement(ns + "DCP",
                new XElement(ns + "HTTP",
                    new XElement(ns + "Get", new XAttribute(XLink + "href", endpoint)))));

        if (SupportsPost(operationName))
        {
            operation.Element(ns + "DCP")?.Element(ns + "HTTP")
                ?.Add(new XElement(ns + "Post", new XAttribute(XLink + "href", endpoint)));
        }

        AddOperationParameters(operation, operationName, ns);

        return operation;
    }

    /// <summary>
    /// Gets the list of supported operations for this protocol.
    /// </summary>
    protected abstract IEnumerable<string> GetSupportedOperations();

    /// <summary>
    /// Adds protocol-specific sections to the root element (e.g., Capability for WMS, Contents for WMTS).
    /// </summary>
    protected abstract Task AddProtocolSpecificSectionsAsync(XElement root, MetadataSnapshot metadata, HttpRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the OWS namespace to use (varies by protocol version).
    /// </summary>
    protected virtual XNamespace GetOwsNamespace() => Ows;

    /// <summary>
    /// Gets the service title from metadata.
    /// </summary>
    protected virtual string GetServiceTitle(MetadataSnapshot metadata)
    {
        var title = metadata.Catalog.Title.IsNullOrWhiteSpace()
            ? metadata.Catalog.Id
            : metadata.Catalog.Title;

        return $"{title} - {GetServiceName()}";
    }

    /// <summary>
    /// Gets the service abstract from metadata.
    /// </summary>
    protected virtual string GetServiceAbstract(MetadataSnapshot metadata)
    {
        return metadata.Catalog.Description.IsNullOrWhiteSpace()
            ? $"Honua {GetServiceName()} Service"
            : metadata.Catalog.Description;
    }

    /// <summary>
    /// Determines if an operation supports POST method.
    /// </summary>
    protected virtual bool SupportsPost(string operationName) => false;

    /// <summary>
    /// Adds protocol-specific extensions to ServiceIdentification.
    /// </summary>
    protected virtual void AddServiceIdentificationExtensions(XElement element, MetadataSnapshot metadata)
    {
        // Default: no extensions
    }

    /// <summary>
    /// Adds protocol-specific extensions to OperationsMetadata.
    /// </summary>
    protected virtual void AddOperationsMetadataExtensions(XElement element, XNamespace ns)
    {
        // Default: no extensions
    }

    /// <summary>
    /// Adds operation-specific parameters (e.g., supported formats, CRS).
    /// </summary>
    protected virtual void AddOperationParameters(XElement operation, string operationName, XNamespace ns)
    {
        // Default: no parameters
    }

    /// <summary>
    /// Gets the encoding for the XML declaration (default: UTF-8).
    /// </summary>
    protected virtual string GetEncoding() => "utf-8";

    /// <summary>
    /// Builds the base URL from the request.
    /// </summary>
    protected string BuildBaseUrl(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    /// <summary>
    /// Builds the endpoint URL for this protocol.
    /// </summary>
    protected abstract string BuildEndpointUrl(HttpRequest request);

    /// <summary>
    /// Writes an OnlineResource element (common across multiple OGC protocols).
    /// </summary>
    protected XElement CreateOnlineResourceElement(string url, XNamespace ns)
    {
        return new XElement(ns + "OnlineResource",
            new XAttribute(XLink + "href", url));
    }

    /// <summary>
    /// Formats a double value for XML output.
    /// </summary>
    protected static string FormatDouble(double value)
    {
        return value.ToString("G", CultureInfo.InvariantCulture);
    }
}
