// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Configuration.V2.Introspection;

/// <summary>
/// Maps database-specific types to Honua configuration field types.
/// </summary>
public static class TypeMapper
{
    /// <summary>
    /// Maps a database type to a Honua field type.
    /// </summary>
    /// <param name="provider">Database provider (postgresql, sqlite, sqlserver, mysql).</param>
    /// <param name="dataType">Database-specific data type name.</param>
    /// <returns>Honua field type (int, string, datetime, double, bool, geometry).</returns>
    public static string MapToHonuaType(string provider, string dataType)
    {
        var normalizedType = dataType.ToLowerInvariant();

        return provider.ToLowerInvariant() switch
        {
            "postgresql" => MapPostgreSqlType(normalizedType),
            "sqlite" => MapSqliteType(normalizedType),
            "sqlserver" or "mssql" => MapSqlServerType(normalizedType),
            "mysql" or "mariadb" => MapMySqlType(normalizedType),
            _ => "string" // Default fallback
        };
    }

    private static string MapPostgreSqlType(string pgType)
    {
        // PostgreSQL type mappings
        return pgType switch
        {
            // Integer types
            "smallint" or "int2" => "int",
            "integer" or "int" or "int4" => "int",
            "bigint" or "int8" => "long",

            // Floating point types
            "real" or "float4" => "float",
            "double precision" or "float8" => "double",
            "numeric" or "decimal" => "decimal",

            // String types
            "character varying" or "varchar" => "string",
            "character" or "char" => "string",
            "text" => "string",

            // Boolean
            "boolean" or "bool" => "bool",

            // Date/Time types
            "timestamp" or "timestamp without time zone" => "datetime",
            "timestamp with time zone" or "timestamptz" => "datetimeoffset",
            "date" => "date",
            "time" or "time without time zone" => "time",

            // UUID
            "uuid" => "guid",

            // JSON
            "json" or "jsonb" => "json",

            // PostGIS geometry types
            var t when t.StartsWith("geometry") => "geometry",
            var t when t.StartsWith("geography") => "geometry",
            "point" => "geometry",
            "linestring" => "geometry",
            "polygon" => "geometry",
            "multipoint" => "geometry",
            "multilinestring" => "geometry",
            "multipolygon" => "geometry",
            "geometrycollection" => "geometry",

            // Binary
            "bytea" => "binary",

            // Fallback
            _ => "string"
        };
    }

    private static string MapSqliteType(string sqliteType)
    {
        // SQLite type mappings (SQLite has dynamic typing)
        return sqliteType switch
        {
            // Integer types
            "integer" or "int" or "tinyint" or "smallint" or "mediumint" or "bigint" => "int",

            // Floating point
            "real" or "double" or "float" => "double",
            "decimal" or "numeric" => "decimal",

            // Text
            "text" or "char" or "varchar" or "clob" => "string",

            // Boolean (stored as integer)
            "boolean" or "bool" => "bool",

            // Date/Time (stored as text or integer)
            "date" or "datetime" or "timestamp" => "datetime",
            "time" => "time",

            // Binary
            "blob" => "binary",

            // SpatiaLite geometry types
            var t when t.StartsWith("geometry") => "geometry",
            "point" => "geometry",
            "linestring" => "geometry",
            "polygon" => "geometry",
            "multipoint" => "geometry",
            "multilinestring" => "geometry",
            "multipolygon" => "geometry",
            "geometrycollection" => "geometry",

            // Fallback
            _ => "string"
        };
    }

    private static string MapSqlServerType(string sqlServerType)
    {
        // SQL Server type mappings
        return sqlServerType switch
        {
            // Integer types
            "tinyint" => "int",
            "smallint" => "int",
            "int" => "int",
            "bigint" => "long",

            // Floating point
            "float" or "real" => "double",
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",

            // String types
            "char" or "varchar" or "text" => "string",
            "nchar" or "nvarchar" or "ntext" => "string",

            // Boolean
            "bit" => "bool",

            // Date/Time types
            "datetime" or "datetime2" or "smalldatetime" => "datetime",
            "datetimeoffset" => "datetimeoffset",
            "date" => "date",
            "time" => "time",

            // UUID
            "uniqueidentifier" => "guid",

            // Binary
            "binary" or "varbinary" or "image" => "binary",

            // SQL Server spatial types
            "geometry" => "geometry",
            "geography" => "geometry",

            // Fallback
            _ => "string"
        };
    }

    private static string MapMySqlType(string mysqlType)
    {
        // MySQL/MariaDB type mappings
        return mysqlType switch
        {
            // Integer types
            "tinyint" or "smallint" or "mediumint" or "int" or "integer" => "int",
            "bigint" => "long",

            // Floating point
            "float" => "float",
            "double" or "real" => "double",
            "decimal" or "numeric" => "decimal",

            // String types
            "char" or "varchar" => "string",
            "text" or "tinytext" or "mediumtext" or "longtext" => "string",

            // Boolean (TINYINT(1))
            "boolean" or "bool" => "bool",

            // Date/Time types
            "datetime" or "timestamp" => "datetime",
            "date" => "date",
            "time" => "time",

            // Binary
            "binary" or "varbinary" or "blob" or "tinyblob" or "mediumblob" or "longblob" => "binary",

            // MySQL spatial types
            "geometry" => "geometry",
            "point" => "geometry",
            "linestring" => "geometry",
            "polygon" => "geometry",
            "multipoint" => "geometry",
            "multilinestring" => "geometry",
            "multipolygon" => "geometry",
            "geometrycollection" => "geometry",

            // JSON
            "json" => "json",

            // Fallback
            _ => "string"
        };
    }

    /// <summary>
    /// Normalizes geometry type names to standard OGC types.
    /// </summary>
    public static string NormalizeGeometryType(string geometryType)
    {
        var normalized = geometryType.ToLowerInvariant()
            .Replace("st_", "")
            .Trim();

        return normalized switch
        {
            "point" => "Point",
            "linestring" or "line" => "LineString",
            "polygon" => "Polygon",
            "multipoint" => "MultiPoint",
            "multilinestring" or "multiline" => "MultiLineString",
            "multipolygon" => "MultiPolygon",
            "geometrycollection" or "geomcollection" => "GeometryCollection",
            "geometry" => "Geometry",
            _ => "Geometry"
        };
    }
}
