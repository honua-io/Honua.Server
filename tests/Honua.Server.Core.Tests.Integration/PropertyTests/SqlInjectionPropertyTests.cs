using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Integration.PropertyTests;

/// <summary>
/// Property-based tests for SQL injection prevention in query building.
/// Uses FsCheck to generate a wide variety of malicious inputs.
/// </summary>
[Trait("Category", "Unit")]
public class SqlInjectionPropertyTests
{
    private static readonly QueryEntityDefinition TestEntity = new(
        "test_table",
        "test_table",
        new Dictionary<string, QueryFieldDefinition>
        {
            ["id"] = new() { Name = "id", DataType = QueryDataType.String, IsKey = true },
            ["name"] = new() { Name = "name", DataType = QueryDataType.String },
            ["value"] = new() { Name = "value", DataType = QueryDataType.Int32 },
            ["created"] = new() { Name = "created", DataType = QueryDataType.DateTimeOffset }
        });

    [Property(MaxTest = 500)]
    public Property SqlFilterTranslator_ShouldNotAllowRawSqlInjection()
    {
        return Prop.ForAll(
            GenerateSqlInjectionAttempt(),
            maliciousInput =>
            {
                var parameters = new Dictionary<string, object?>();
                var translator = new SqlFilterTranslator(
                    TestEntity,
                    parameters,
                    QuotePostgresIdentifier,
                    "test");

                var filter = new QueryFilter(
                    new QueryBinaryExpression(
                        new QueryFieldReference("name"),
                        QueryBinaryOperator.Equal,
                        new QueryConstant(maliciousInput)));

                var sql = translator.Translate(filter, "t");

                // Verify parameterization: malicious input should be in parameters, not in SQL
                Assert.NotNull(sql);
                Assert.Contains("@test_0", sql);
                Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DELETE", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("UPDATE", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("INSERT", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("--", sql);
                Assert.DoesNotContain("/*", sql);
                Assert.DoesNotContain("xp_", sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("EXEC", sql, StringComparison.OrdinalIgnoreCase);

                // Malicious content should be in parameters only
                Assert.Contains(parameters, p => p.Value?.ToString()?.Contains(maliciousInput) ?? false);

                return true;
            });
    }

    [Property(MaxTest = 500)]
    public Property FieldNames_ShouldBeQuotedAndSanitized()
    {
        return Prop.ForAll(
            Arb.From(Arb.Default.NonNull<string>().Generator.Select(nn => nn.Get)),
            new Func<string, bool>(fieldName =>
            {
                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    return true;
                }

                var quoted = QuotePostgresIdentifier(fieldName);

                // Quoted identifiers should start and end with quotes
                Assert.StartsWith("\"", quoted);
                Assert.EndsWith("\"", quoted);

                // For dot-separated identifiers, each part is quoted separately
                // e.g., "schema"."table" is valid PostgreSQL syntax
                // Split by the pattern "." to get individual quoted parts
                var quotedParts = quoted.Split(new[] { "\".\"" }, StringSplitOptions.None);

                foreach (var quotedPart in quotedParts)
                {
                    // Remove outer quotes from each part (first and last may have them)
                    var part = quotedPart.Trim('"');

                    // Within each part, any quotes should be escaped (doubled)
                    if (part.Contains('"'))
                    {
                        // Replace all doubled quotes and check no single quotes remain
                        var withoutDoubled = part.Replace("\"\"", "");
                        Assert.DoesNotContain('"', withoutDoubled);
                    }
                }

                return true;
            }));
    }

    [Property(MaxTest = 300)]
    public Property LikePatterns_ShouldBeParameterized()
    {
        return Prop.ForAll(
            GenerateLikeInjectionAttempt(),
            maliciousPattern =>
            {
                var parameters = new Dictionary<string, object?>();
                var translator = new SqlFilterTranslator(
                    TestEntity,
                    parameters,
                    QuotePostgresIdentifier,
                    "like_test");

                var filter = new QueryFilter(
                    new QueryFunctionExpression(
                        "like",
                        new QueryExpression[] {
                            new QueryFieldReference("name"),
                            new QueryConstant(maliciousPattern)
                        }));

                var sql = translator.Translate(filter, "t");

                Assert.NotNull(sql);
                Assert.Contains("LIKE", sql);
                Assert.Contains("@like_test_", sql);

                // Pattern should be parameterized
                Assert.Single(parameters);

                return true;
            });
    }

    [Property(MaxTest = 300)]
    public Property NumericFields_ShouldRejectNonNumericSqlInjection()
    {
        return Prop.ForAll(
            GenerateSqlInjectionAttempt(),
            maliciousInput =>
            {
                var parameters = new Dictionary<string, object?>();
                var translator = new SqlFilterTranslator(
                    TestEntity,
                    parameters,
                    QuotePostgresIdentifier,
                    "num_test");

                var filter = new QueryFilter(
                    new QueryBinaryExpression(
                        new QueryFieldReference("value"),
                        QueryBinaryOperator.Equal,
                        new QueryConstant(maliciousInput)));

                // Should throw when trying to convert SQL injection attempt to Int32
                Assert.Throws<InvalidOperationException>(() =>
                    translator.Translate(filter, "t"));

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property MultipleParameters_ShouldAllBeParameterized()
    {
        return Prop.ForAll(
            GenerateMaliciousStringPair(),
            tuple =>
            {
                var (input1, input2) = tuple;
                var parameters = new Dictionary<string, object?>();
                var translator = new SqlFilterTranslator(
                    TestEntity,
                    parameters,
                    QuotePostgresIdentifier,
                    "multi");

                var filter = new QueryFilter(
                    new QueryBinaryExpression(
                        new QueryBinaryExpression(
                            new QueryFieldReference("name"),
                            QueryBinaryOperator.Equal,
                            new QueryConstant(input1)),
                        QueryBinaryOperator.And,
                        new QueryBinaryExpression(
                            new QueryFieldReference("id"),
                            QueryBinaryOperator.Equal,
                            new QueryConstant(input2))));

                var sql = translator.Translate(filter, "t");

                Assert.NotNull(sql);
                Assert.Equal(2, parameters.Count);
                Assert.Contains("@multi_0", sql);
                Assert.Contains("@multi_1", sql);

                return true;
            });
    }

    [Property(MaxTest = 200)]
    public Property DateTimeFields_ShouldRejectMalformedDateInjection()
    {
        return Prop.ForAll(
            GenerateSqlInjectionAttempt(),
            maliciousInput =>
            {
                var parameters = new Dictionary<string, object?>();
                var translator = new SqlFilterTranslator(
                    TestEntity,
                    parameters,
                    QuotePostgresIdentifier,
                    "date_test");

                var filter = new QueryFilter(
                    new QueryBinaryExpression(
                        new QueryFieldReference("created"),
                        QueryBinaryOperator.GreaterThan,
                        new QueryConstant(maliciousInput)));

                // Should throw when trying to convert SQL injection to DateTimeOffset
                var exception = Record.Exception(() => translator.Translate(filter, "t"));
                Assert.NotNull(exception);

                return true;
            });
    }

    // FsCheck Generators

    private static Arbitrary<string> GenerateSqlInjectionAttempt()
    {
        var commonInjections = new[]
        {
            "'; DROP TABLE users--",
            "1' OR '1'='1",
            "'; DELETE FROM test; --",
            "admin'--",
            "' OR 1=1--",
            "1'; DROP TABLE test--",
            "'; EXEC sp_MSForEachTable 'DROP TABLE ?'--",
            "1 UNION SELECT * FROM users--",
            "' OR 'a'='a",
            "1'; UPDATE users SET admin=1--",
            "'; WAITFOR DELAY '00:00:05'--",
            "1' AND '1'='2' UNION SELECT * FROM information_schema.tables--",
            "admin' OR '1'='1'/*",
            "'; SELECT * FROM sys.databases--",
            "1'; SHUTDOWN--",
            "' OR EXISTS(SELECT * FROM users)--",
            "1' AND SLEEP(5)--",
            "'; xp_cmdshell('dir')--",
            "\"; DROP TABLE users--",
            "1\"; DELETE FROM test--",
            "' UNION ALL SELECT NULL,NULL,NULL--",
            "admin' OR 1=1#",
            "' OR 'x'='x",
            "1'; INSERT INTO users VALUES('hacked')--",
            "' OR username IS NOT NULL--",
            "1' GROUP BY columnname HAVING 1=1--",
            "' OR ''='",
            "1'; TRUNCATE TABLE logs--",
            "' OR 1=1 LIMIT 1--",
            "1' ORDER BY 1--",
            "/**/OR/**/1=1--",
            "1' UNI/**/ON SELECT--",
            "' OR 'a' BETWEEN 'a' AND 'z'--",
            "1'; DECLARE @q varchar(8000)--",
            "' OR CHAR(65)=CHAR(65)--",
            "%' OR '1'='1",
            "'; CREATE USER hacker--",
            "' OR EXISTS(SELECT 1)--",
            "1' AND ASCII(SUBSTRING((SELECT TOP 1 name FROM sysobjects),1,1))>1--"
        };

        var gen = Gen.Frequency<string>(
            new WeightAndValue<Gen<string>>(8, Gen.Elements(commonInjections)),
            new WeightAndValue<Gen<string>>(1, Gen.Constant("' OR 1=1--")),
            new WeightAndValue<Gen<string>>(1, Gen.Constant("; DROP TABLE test;")));

        return Arb.From(gen);
    }

    private static Arbitrary<string> GenerateLikeInjectionAttempt()
    {
        var likeInjections = new[]
        {
            "%' OR '1'='1",
            "%'; DROP TABLE users--",
            "_%' OR 1=1--",
            "%' UNION SELECT * FROM users WHERE '1'='1",
            "[%' OR '1'='1",
            "^%' OR 1=1--",
            "%' AND '1'='1"
        };

        return Arb.From(Gen.Elements(likeInjections));
    }

    private static Arbitrary<(string, string)> GenerateMaliciousStringPair()
    {
        var gen = from s1 in GenerateSqlInjectionAttempt().Generator
                  from s2 in GenerateSqlInjectionAttempt().Generator
                  select (s1, s2);

        return Arb.From(gen);
    }

    private static string QuotePostgresIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return "\"\"";
        }

        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.StartsWith("\"", StringComparison.Ordinal) && part.EndsWith("\"", StringComparison.Ordinal) && part.Length >= 2)
            {
                part = part[1..^1].Replace("\"\"", "\"");
            }
            else if (part.StartsWith("`", StringComparison.Ordinal) && part.EndsWith("`", StringComparison.Ordinal) && part.Length >= 2)
            {
                part = part[1..^1].Replace("``", "`");
            }
            else if (part.StartsWith("[", StringComparison.Ordinal) && part.EndsWith("]", StringComparison.Ordinal) && part.Length >= 2)
            {
                part = part[1..^1].Replace("]]", "]");
            }

            parts[i] = $"\"{part.Replace("\"", "\"\"")}\"";
        }

        return parts.Length == 0 ? "\"\"" : string.Join('.', parts);
    }
}
