// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Simplified HCL parser for Honua configuration files.
/// Supports basic HCL syntax: blocks, attributes, strings, numbers, booleans, lists.
/// </summary>
public sealed class HclParser
{
    private string _input = string.Empty;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    /// <summary>
    /// Parse HCL content into a HonuaConfig object.
    /// </summary>
    public HonuaConfig Parse(string hclContent)
    {
        if (string.IsNullOrWhiteSpace(hclContent))
        {
            throw new ArgumentException("HCL content cannot be empty.", nameof(hclContent));
        }

        _input = hclContent;
        _position = 0;
        _line = 1;
        _column = 1;

        var config = new HonuaConfig();

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd()) break;

            var token = PeekWord();

            switch (token)
            {
                case "honua":
                    ParseHonuaBlock(config);
                    break;

                case "data_source":
                    ParseDataSourceBlock(config);
                    break;

                case "service":
                    ParseServiceBlock(config);
                    break;

                case "layer":
                    ParseLayerBlock(config);
                    break;

                case "cache":
                    ParseCacheBlock(config);
                    break;

                case "rate_limit":
                    ParseRateLimitBlock(config);
                    break;

                case "variable":
                case "var":
                    ParseVariableDeclaration(config);
                    break;

                default:
                    throw new ParseException($"Unexpected token '{token}' at line {_line}, column {_column}");
            }
        }

        return config;
    }

    private void ParseHonuaBlock(HonuaConfig config)
    {
        Expect("honua");
        Expect("{");

        var settings = new HonuaGlobalSettings();
        var corsSettings = new CorsSettings();
        bool hasCors = false;

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "version":
                    settings = settings with { Version = ReadString() };
                    break;
                case "environment":
                    settings = settings with { Environment = ReadString() };
                    break;
                case "log_level":
                    settings = settings with { LogLevel = ReadString() };
                    break;
                case "allowed_hosts":
                    settings = settings with { AllowedHosts = ReadStringList() };
                    break;
                case "cors":
                    hasCors = true;
                    corsSettings = ParseCorsBlock();
                    break;
                default:
                    throw new ParseException($"Unknown honua setting '{key}' at line {_line}");
            }
        }

        if (hasCors)
        {
            settings = settings with { Cors = corsSettings };
        }

        config.Honua = settings;
        Expect("}");
    }

    private CorsSettings ParseCorsBlock()
    {
        Expect("{");

        var cors = new CorsSettings();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "allow_any_origin":
                    cors = cors with { AllowAnyOrigin = ReadBoolean() };
                    break;
                case "allowed_origins":
                    cors = cors with { AllowedOrigins = ReadStringList() };
                    break;
                case "allow_credentials":
                    cors = cors with { AllowCredentials = ReadBoolean() };
                    break;
                default:
                    throw new ParseException($"Unknown CORS setting '{key}' at line {_line}");
            }
        }

        Expect("}");
        return cors;
    }

    private void ParseDataSourceBlock(HonuaConfig config)
    {
        Expect("data_source");
        var id = ReadString();
        Expect("{");

        var dataSource = new DataSourceBlock { Id = id, Provider = "", Connection = "" };
        PoolSettings? pool = null;

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "provider":
                    dataSource = dataSource with { Provider = ReadString() };
                    break;
                case "connection":
                    dataSource = dataSource with { Connection = ReadStringOrExpression() };
                    break;
                case "health_check":
                    dataSource = dataSource with { HealthCheck = ReadString() };
                    break;
                case "pool":
                    pool = ParsePoolBlock();
                    break;
                default:
                    throw new ParseException($"Unknown data_source setting '{key}' at line {_line}");
            }
        }

        dataSource = dataSource with { Pool = pool };
        config.DataSources[id] = dataSource;
        Expect("}");
    }

    private PoolSettings ParsePoolBlock()
    {
        Expect("{");

        var pool = new PoolSettings();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "min_size":
                    pool = pool with { MinSize = ReadInteger() };
                    break;
                case "max_size":
                    pool = pool with { MaxSize = ReadInteger() };
                    break;
                case "timeout":
                    pool = pool with { Timeout = ReadInteger() };
                    break;
                default:
                    throw new ParseException($"Unknown pool setting '{key}' at line {_line}");
            }
        }

        Expect("}");
        return pool;
    }

    private void ParseServiceBlock(HonuaConfig config)
    {
        Expect("service");
        var id = ReadString();
        Expect("{");

        var service = new ServiceBlock { Id = id, Type = id, Enabled = true };
        var settings = new Dictionary<string, object?>();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            if (key == "enabled")
            {
                service = service with { Enabled = ReadBoolean() };
            }
            else if (key == "type")
            {
                service = service with { Type = ReadString() };
            }
            else
            {
                // Store as generic setting
                settings[key] = ReadValue();
            }
        }

        service = service with { Settings = settings };
        config.Services[id] = service;
        Expect("}");
    }

    private void ParseLayerBlock(HonuaConfig config)
    {
        Expect("layer");
        var id = ReadString();
        Expect("{");

        var layer = new LayerBlock
        {
            Id = id,
            Title = "",
            DataSource = "",
            Table = "",
            IdField = ""
        };

        GeometrySettings? geometry = null;
        Dictionary<string, FieldDefinition>? fields = null;
        var services = new List<string>();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "title":
                    layer = layer with { Title = ReadString() };
                    break;
                case "data_source":
                    layer = layer with { DataSource = ReadReference() };
                    break;
                case "table":
                    layer = layer with { Table = ReadString() };
                    break;
                case "description":
                    layer = layer with { Description = ReadString() };
                    break;
                case "id_field":
                    layer = layer with { IdField = ReadString() };
                    break;
                case "display_field":
                    layer = layer with { DisplayField = ReadString() };
                    break;
                case "introspect_fields":
                    layer = layer with { IntrospectFields = ReadBoolean() };
                    break;
                case "geometry":
                    geometry = ParseGeometryBlock();
                    break;
                case "fields":
                    fields = ParseFieldsBlock();
                    break;
                case "services":
                    services = ReadReferenceList();
                    break;
                default:
                    throw new ParseException($"Unknown layer setting '{key}' at line {_line}");
            }
        }

        layer = layer with
        {
            Geometry = geometry,
            Fields = fields,
            Services = services
        };

        config.Layers[id] = layer;
        Expect("}");
    }

    private GeometrySettings ParseGeometryBlock()
    {
        Expect("{");

        var geometry = new GeometrySettings { Column = "", Type = "" };

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "column":
                    geometry = geometry with { Column = ReadString() };
                    break;
                case "type":
                    geometry = geometry with { Type = ReadString() };
                    break;
                case "srid":
                    geometry = geometry with { Srid = ReadInteger() };
                    break;
                default:
                    throw new ParseException($"Unknown geometry setting '{key}' at line {_line}");
            }
        }

        Expect("}");
        return geometry;
    }

    private Dictionary<string, FieldDefinition> ParseFieldsBlock()
    {
        Expect("{");

        var fields = new Dictionary<string, FieldDefinition>();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var fieldName = ReadWord();
            Expect("=");
            Expect("{");

            var field = new FieldDefinition { Type = "" };

            while (!Check("}"))
            {
                SkipWhitespaceAndComments();
                if (Check("}")) break;

                var key = ReadWord();
                Expect("=");

                switch (key)
                {
                    case "type":
                        field = field with { Type = ReadString() };
                        break;
                    case "nullable":
                        field = field with { Nullable = ReadBoolean() };
                        break;
                    default:
                        throw new ParseException($"Unknown field setting '{key}' at line {_line}");
                }
            }

            Expect("}");
            fields[fieldName] = field;
        }

        Expect("}");
        return fields;
    }

    private void ParseCacheBlock(HonuaConfig config)
    {
        Expect("cache");
        var id = ReadString();
        Expect("{");

        var cache = new CacheBlock { Id = id, Type = "memory", Enabled = true };

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "type":
                    cache = cache with { Type = ReadString() };
                    break;
                case "enabled":
                    cache = cache with { Enabled = ReadBoolean() };
                    break;
                case "connection":
                    cache = cache with { Connection = ReadStringOrExpression() };
                    break;
                case "required_in":
                    cache = cache with { RequiredIn = ReadStringList() };
                    break;
                default:
                    throw new ParseException($"Unknown cache setting '{key}' at line {_line}");
            }
        }

        config.Caches[id] = cache;
        Expect("}");
    }

    private void ParseRateLimitBlock(HonuaConfig config)
    {
        Expect("rate_limit");
        Expect("{");

        var rateLimit = new RateLimitBlock();
        var rules = new Dictionary<string, RateLimitRule>();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var key = ReadWord();
            Expect("=");

            switch (key)
            {
                case "enabled":
                    rateLimit = rateLimit with { Enabled = ReadBoolean() };
                    break;
                case "store":
                    rateLimit = rateLimit with { Store = ReadString() };
                    break;
                case "rules":
                    rules = ParseRateLimitRulesBlock();
                    break;
                default:
                    throw new ParseException($"Unknown rate_limit setting '{key}' at line {_line}");
            }
        }

        rateLimit = rateLimit with { Rules = rules };
        config.RateLimit = rateLimit;
        Expect("}");
    }

    private Dictionary<string, RateLimitRule> ParseRateLimitRulesBlock()
    {
        Expect("{");

        var rules = new Dictionary<string, RateLimitRule>();

        while (!Check("}"))
        {
            SkipWhitespaceAndComments();
            if (Check("}")) break;

            var ruleName = ReadWord();
            Expect("=");
            Expect("{");

            var rule = new RateLimitRule();

            while (!Check("}"))
            {
                SkipWhitespaceAndComments();
                if (Check("}")) break;

                var key = ReadWord();
                Expect("=");

                switch (key)
                {
                    case "requests":
                        rule = rule with { Requests = ReadInteger() };
                        break;
                    case "window":
                        rule = rule with { Window = ReadString() };
                        break;
                    default:
                        throw new ParseException($"Unknown rule setting '{key}' at line {_line}");
                }
            }

            Expect("}");
            rules[ruleName] = rule;
        }

        Expect("}");
        return rules;
    }

    private void ParseVariableDeclaration(HonuaConfig config)
    {
        var keyword = ReadWord(); // "variable" or "var"
        var varName = ReadString();
        Expect("=");
        var value = ReadValue();

        config.Variables[varName] = value;
    }

    // Tokenizer/Lexer helpers

    private bool IsAtEnd() => _position >= _input.Length;

    private char Peek() => IsAtEnd() ? '\0' : _input[_position];

    private char Advance()
    {
        var ch = _input[_position++];
        if (ch == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
        return ch;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            var ch = Peek();

            if (char.IsWhiteSpace(ch))
            {
                Advance();
            }
            else if (ch == '#' || (ch == '/' && _position + 1 < _input.Length && _input[_position + 1] == '/'))
            {
                // Skip line comment
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }
            }
            else if (ch == '/' && _position + 1 < _input.Length && _input[_position + 1] == '*')
            {
                // Skip block comment
                Advance(); // '/'
                Advance(); // '*'
                while (!IsAtEnd())
                {
                    if (Peek() == '*' && _position + 1 < _input.Length && _input[_position + 1] == '/')
                    {
                        Advance(); // '*'
                        Advance(); // '/'
                        break;
                    }
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private string PeekWord()
    {
        var startPos = _position;
        SkipWhitespaceAndComments();

        var sb = new StringBuilder();
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Peek());
            _position++;
        }

        var word = sb.ToString();
        _position = startPos; // Reset position
        SkipWhitespaceAndComments();
        return word;
    }

    private string ReadWord()
    {
        SkipWhitespaceAndComments();

        var sb = new StringBuilder();
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Advance());
        }

        return sb.ToString();
    }

    private string ReadString()
    {
        SkipWhitespaceAndComments();

        var quote = Peek();
        if (quote != '"' && quote != '\'')
        {
            throw new ParseException($"Expected string at line {_line}, column {_column}");
        }

        Advance(); // Skip opening quote

        var sb = new StringBuilder();
        while (!IsAtEnd() && Peek() != quote)
        {
            if (Peek() == '\\')
            {
                Advance();
                if (!IsAtEnd())
                {
                    var escaped = Advance();
                    sb.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escaped
                    });
                }
            }
            else
            {
                sb.Append(Advance());
            }
        }

        if (IsAtEnd())
        {
            throw new ParseException($"Unterminated string at line {_line}");
        }

        Advance(); // Skip closing quote
        return sb.ToString();
    }

    private string ReadStringOrExpression()
    {
        // Check if it's a function call like env("VAR")
        var word = PeekWord();
        if (word == "env" || word == "var")
        {
            var functionName = ReadWord();
            Expect("(");
            var argument = ReadString();
            Expect(")");
            return functionName + "(" + argument + ")";
        }

        return ReadString();
    }

    private string ReadReference()
    {
        // Read a reference like data_source.sqlite-test or just "sqlite-test"
        SkipWhitespaceAndComments();

        if (Peek() == '"' || Peek() == '\'')
        {
            return ReadString();
        }

        var sb = new StringBuilder();
        while (!IsAtEnd() && (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '.' || Peek() == '-'))
        {
            sb.Append(Advance());
        }

        return sb.ToString();
    }

    private List<string> ReadReferenceList()
    {
        Expect("[");

        var list = new List<string>();

        while (!Check("]"))
        {
            SkipWhitespaceAndComments();
            if (Check("]")) break;

            list.Add(ReadReference());

            SkipWhitespaceAndComments();
            if (Check(","))
            {
                Advance();
            }
        }

        Expect("]");
        return list;
    }

    private List<string> ReadStringList()
    {
        Expect("[");

        var list = new List<string>();

        while (!Check("]"))
        {
            SkipWhitespaceAndComments();
            if (Check("]")) break;

            list.Add(ReadString());

            SkipWhitespaceAndComments();
            if (Check(","))
            {
                Advance();
            }
        }

        Expect("]");
        return list;
    }

    private int ReadInteger()
    {
        SkipWhitespaceAndComments();

        var sb = new StringBuilder();
        if (Peek() == '-')
        {
            sb.Append(Advance());
        }

        while (!IsAtEnd() && char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }

        if (!int.TryParse(sb.ToString(), out var value))
        {
            throw new ParseException($"Invalid integer '{sb}' at line {_line}");
        }

        return value;
    }

    private bool ReadBoolean()
    {
        SkipWhitespaceAndComments();

        var word = ReadWord();
        return word.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => throw new ParseException($"Expected boolean (true/false) but got '{word}' at line {_line}")
        };
    }

    private object? ReadValue()
    {
        SkipWhitespaceAndComments();

        var ch = Peek();

        // String
        if (ch == '"' || ch == '\'')
        {
            return ReadString();
        }

        // List
        if (ch == '[')
        {
            return ReadStringList();
        }

        // Boolean or number
        var word = PeekWord();
        if (word == "true" || word == "false")
        {
            return ReadBoolean();
        }

        // Try integer
        if (char.IsDigit(ch) || ch == '-')
        {
            return ReadInteger();
        }

        throw new ParseException($"Unexpected value at line {_line}, column {_column}");
    }

    private bool Check(string expected)
    {
        SkipWhitespaceAndComments();

        if (_position + expected.Length > _input.Length)
        {
            return false;
        }

        return _input.Substring(_position, expected.Length) == expected;
    }

    private void Expect(string expected)
    {
        SkipWhitespaceAndComments();

        if (!Check(expected))
        {
            throw new ParseException($"Expected '{expected}' at line {_line}, column {_column}");
        }

        _position += expected.Length;
        _column += expected.Length;
    }
}

/// <summary>
/// Exception thrown during HCL parsing.
/// </summary>
public sealed class ParseException : Exception
{
    public ParseException(string message) : base(message) { }
}
