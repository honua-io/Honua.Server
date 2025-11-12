// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.MapSDK.Models.Esri;

/// <summary>
/// Base class for Esri domains
/// </summary>
public class EsriDomain
{
    /// <summary>
    /// Domain type (codedValue, range, or inherited)
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Domain name
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Coded value domain (pick list)
/// </summary>
public class EsriCodedValueDomain : EsriDomain
{
    /// <summary>
    /// Coded values
    /// </summary>
    [JsonPropertyName("codedValues")]
    public List<EsriCodedValue>? CodedValues { get; set; }

    public EsriCodedValueDomain()
    {
        Type = "codedValue";
    }
}

/// <summary>
/// Coded value (code/name pair)
/// </summary>
public class EsriCodedValue
{
    /// <summary>
    /// Code (actual value stored)
    /// </summary>
    [JsonPropertyName("code")]
    public object? Code { get; set; }

    /// <summary>
    /// Name (display value)
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Range domain (numeric range)
/// </summary>
public class EsriRangeDomain : EsriDomain
{
    /// <summary>
    /// Minimum value
    /// </summary>
    [JsonPropertyName("range")]
    public double[] Range { get; set; } = new double[2];

    public EsriRangeDomain()
    {
        Type = "range";
    }

    /// <summary>
    /// Minimum value
    /// </summary>
    public double MinValue
    {
        get => Range[0];
        set => Range[0] = value;
    }

    /// <summary>
    /// Maximum value
    /// </summary>
    public double MaxValue
    {
        get => Range[1];
        set => Range[1] = value;
    }
}

/// <summary>
/// Inherited domain
/// </summary>
public class EsriInheritedDomain : EsriDomain
{
    public EsriInheritedDomain()
    {
        Type = "inherited";
    }
}
