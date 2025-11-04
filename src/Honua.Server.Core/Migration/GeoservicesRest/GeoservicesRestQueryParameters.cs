// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestQueryParameters
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase)
    {
        { "f", "json" }
    };

    public string Where
    {
        get => Get("where") ?? "1=1";
        set => Set("where", value.IsNullOrWhiteSpace() ? "1=1" : value);
    }

    public bool ReturnGeometry
    {
        get => string.Equals(Get("returnGeometry"), "true", StringComparison.OrdinalIgnoreCase);
        set => Set("returnGeometry", value ? "true" : "false");
    }

    public string OutFields
    {
        get => Get("outFields") ?? "*";
        set => Set("outFields", value.IsNullOrWhiteSpace() ? "*" : value);
    }

    public int? ResultOffset
    {
        get => TryGetInt("resultOffset");
        set => SetInt("resultOffset", value);
    }

    public int? ResultRecordCount
    {
        get => TryGetInt("resultRecordCount");
        set => SetInt("resultRecordCount", value);
    }

    public string? ObjectIds
    {
        get => Get("objectIds");
        set => Set("objectIds", value);
    }

    public bool? ReturnIdsOnly
    {
        get => TryGetBool("returnIdsOnly");
        set => SetBool("returnIdsOnly", value);
    }

    public bool? ReturnCountOnly
    {
        get => TryGetBool("returnCountOnly");
        set => SetBool("returnCountOnly", value);
    }

    public string? Time
    {
        get => Get("time");
        set => Set("time", value);
    }

    public int? OutSpatialReference
    {
        get => TryGetInt("outSR");
        set => SetInt("outSR", value);
    }

    public string? AdditionalParameter(string key)
    {
        Guard.NotNullOrWhiteSpace(key);
        return Get(key);
    }

    public void SetAdditionalParameter(string key, string? value)
    {
        Guard.NotNullOrWhiteSpace(key);
        Set(key, value);
    }

    public IReadOnlyDictionary<string, string?> GetValues()
    {
        return _values;
    }

    private string? Get(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    private void Set(string key, string? value)
    {
        if (value.IsNullOrEmpty())
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value;
        }
    }

    private int? TryGetInt(string key)
    {
        if (!_values.TryGetValue(key, out var value) || value.IsNullOrWhiteSpace())
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private bool? TryGetBool(string key)
    {
        if (!_values.TryGetValue(key, out var value) || value.IsNullOrWhiteSpace())
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private void SetInt(string key, int? value)
    {
        Set(key, value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : null);
    }

    private void SetBool(string key, bool? value)
    {
        if (value is null)
        {
            _values.Remove(key);
        }
        else
        {
            _values[key] = value.Value ? "true" : "false";
        }
    }
}
