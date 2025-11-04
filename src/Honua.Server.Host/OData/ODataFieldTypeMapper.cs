// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.OData.Edm;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public interface IODataFieldTypeMapper
{
    IEdmPrimitiveTypeReference GetKeyType(LayerDefinition layer);
    IEdmPrimitiveTypeReference GetGeometryType(LayerDefinition layer);
    bool TryGetPrimitiveType(FieldDefinition field, out IEdmPrimitiveTypeReference typeReference);
}

public sealed class ODataFieldTypeMapper : IODataFieldTypeMapper
{
    private static readonly IEdmPrimitiveTypeReference DefaultStringType =
        EdmCoreModel.Instance.GetString(isNullable: true);

    public IEdmPrimitiveTypeReference GetKeyType(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        var keyField = FieldMetadataResolver.FindField(layer, layer.IdField);

        if (keyField is not null && TryGetPrimitiveType(keyField, out var keyType))
        {
            return EnsureNonNullable(keyType);
        }

        return EdmCoreModel.Instance.GetString(isNullable: false);
    }

    public IEdmPrimitiveTypeReference GetGeometryType(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        return DefaultStringType;
    }

    public bool TryGetPrimitiveType(FieldDefinition field, out IEdmPrimitiveTypeReference typeReference)
    {
        Guard.NotNull(field);

        var nullable = field.Nullable;
        var token = (field.DataType ?? field.StorageType ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            typeReference = DefaultStringType;
            return true;
        }

        token = token.ToLowerInvariant();

        typeReference = token switch
        {
            "byte" => EdmCoreModel.Instance.GetByte(nullable),
            "sbyte" => EdmCoreModel.Instance.GetSByte(nullable),
            "int16" or "smallint" => EdmCoreModel.Instance.GetInt16(nullable),
            "int" or "int32" or "integer" => EdmCoreModel.Instance.GetInt32(nullable),
            "long" or "int64" or "bigint" => EdmCoreModel.Instance.GetInt64(nullable),
            "single" or "float" or "real" => EdmCoreModel.Instance.GetSingle(nullable),
            "double" => EdmCoreModel.Instance.GetDouble(nullable),
            "decimal" or "numeric" => CreateDecimalType(field, nullable),
            "bool" or "boolean" => EdmCoreModel.Instance.GetBoolean(nullable),
            "guid" or "uniqueidentifier" => EdmCoreModel.Instance.GetGuid(nullable),
            "datetimeoffset" or "datetime" or "timestamp" => EdmCoreModel.Instance.GetDateTimeOffset(nullable),
            "date" => EdmCoreModel.Instance.GetDate(nullable),
            "time" or "timeofday" => EdmCoreModel.Instance.GetTimeOfDay(nullable),
            _ => EdmCoreModel.Instance.GetString(nullable)
        };

        return true;
    }

    private static IEdmPrimitiveTypeReference CreateDecimalType(FieldDefinition field, bool nullable)
    {
        var precision = field.Precision;
        var scale = field.Scale;

        if (precision.HasValue)
        {
            var normalizedScale = scale.HasValue ? Math.Max(0, scale.Value) : (int?)null;
            return EdmCoreModel.Instance.GetDecimal(precision.Value, normalizedScale, nullable);
        }

        return EdmCoreModel.Instance.GetDecimal(nullable);
    }

    private static IEdmPrimitiveTypeReference EnsureNonNullable(IEdmPrimitiveTypeReference typeReference)
    {
        if (!typeReference.IsNullable)
        {
            return typeReference;
        }

        return typeReference.PrimitiveKind() switch
        {
            EdmPrimitiveTypeKind.String => EdmCoreModel.Instance.GetString(isNullable: false),
            EdmPrimitiveTypeKind.Decimal => EdmCoreModel.Instance.GetDecimal(typeReference.AsDecimal().Precision, typeReference.AsDecimal().Scale, false),
            EdmPrimitiveTypeKind.Binary => EdmCoreModel.Instance.GetBinary(false),
            EdmPrimitiveTypeKind.Byte => EdmCoreModel.Instance.GetByte(false),
            EdmPrimitiveTypeKind.SByte => EdmCoreModel.Instance.GetSByte(false),
            EdmPrimitiveTypeKind.Int16 => EdmCoreModel.Instance.GetInt16(false),
            EdmPrimitiveTypeKind.Int32 => EdmCoreModel.Instance.GetInt32(false),
            EdmPrimitiveTypeKind.Int64 => EdmCoreModel.Instance.GetInt64(false),
            EdmPrimitiveTypeKind.Single => EdmCoreModel.Instance.GetSingle(false),
            EdmPrimitiveTypeKind.Double => EdmCoreModel.Instance.GetDouble(false),
            EdmPrimitiveTypeKind.Boolean => EdmCoreModel.Instance.GetBoolean(false),
            EdmPrimitiveTypeKind.Guid => EdmCoreModel.Instance.GetGuid(false),
            EdmPrimitiveTypeKind.DateTimeOffset => EdmCoreModel.Instance.GetDateTimeOffset(false),
            EdmPrimitiveTypeKind.Date => EdmCoreModel.Instance.GetDate(false),
            EdmPrimitiveTypeKind.TimeOfDay => EdmCoreModel.Instance.GetTimeOfDay(false),
            _ => EdmCoreModel.Instance.GetString(false)
        };
    }
}

