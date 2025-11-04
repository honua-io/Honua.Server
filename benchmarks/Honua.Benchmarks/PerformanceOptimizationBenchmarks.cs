using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Honua.Server.Core.Performance;

namespace Honua.Benchmarks;

/// <summary>
/// Benchmarks for performance optimizations implemented in the codebase.
/// Demonstrates the performance improvements from:
/// - ArrayPool usage
/// - StringBuilder pooling
/// - Span-based parsing
/// - Static lambdas
/// - LINQ optimizations
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PerformanceOptimizationBenchmarks
{
    private readonly string[] _columnNames = Enumerable.Range(0, 20).Select(i => $"column_{i}").ToArray();
    private readonly byte[] _testData = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
    private readonly List<string> _testStrings = Enumerable.Range(0, 100).Select(i => $"test_string_{i}").ToList();

    #region String Concatenation Benchmarks

    [Benchmark(Baseline = true, Description = "String concatenation (baseline)")]
    public string StringConcatenation_Baseline()
    {
        string result = "";
        for (int i = 0; i < _columnNames.Length; i++)
        {
            if (i > 0) result += ", ";
            result += "\"" + _columnNames[i] + "\"";
        }
        return result;
    }

    [Benchmark(Description = "String concatenation with StringBuilder")]
    public string StringConcatenation_StringBuilder()
    {
        var sb = new StringBuilder(_columnNames.Length * 20);
        for (int i = 0; i < _columnNames.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(_columnNames[i]).Append('"');
        }
        return sb.ToString();
    }

    [Benchmark(Description = "String concatenation with pooled StringBuilder")]
    public string StringConcatenation_PooledStringBuilder()
    {
        var sb = ObjectPools.StringBuilder.Get();
        try
        {
            for (int i = 0; i < _columnNames.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(_columnNames[i]).Append('"');
            }
            return sb.ToString();
        }
        finally
        {
            ObjectPools.StringBuilder.Return(sb);
        }
    }

    #endregion

    #region Array Allocation Benchmarks

    [Benchmark(Baseline = true, Description = "Standard array allocation (baseline)")]
    public byte[] ArrayAllocation_Baseline()
    {
        var buffer = new byte[1024];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 256);
        }
        return buffer;
    }

    [Benchmark(Description = "ArrayPool allocation")]
    public byte[] ArrayAllocation_ArrayPool()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1024);
        try
        {
            for (int i = 0; i < 1024; i++)
            {
                buffer[i] = (byte)(i % 256);
            }
            var result = new byte[1024];
            Array.Copy(buffer, result, 1024);
            return result;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    [Benchmark(Description = "Stackalloc for small buffers")]
    public byte[] ArrayAllocation_Stackalloc()
    {
        Span<byte> buffer = stackalloc byte[1024];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 256);
        }
        return buffer.ToArray();
    }

    #endregion

    #region LINQ Optimization Benchmarks

    [Benchmark(Baseline = true, Description = "LINQ Where().Any() (baseline)")]
    public bool LinqOptimization_WhereAny_Baseline()
    {
        return _testStrings.Where(s => s.Contains("50")).Any();
    }

    [Benchmark(Description = "LINQ Any(predicate)")]
    public bool LinqOptimization_AnyPredicate()
    {
        return _testStrings.Any(s => s.Contains("50"));
    }

    [Benchmark(Baseline = true, Description = "LINQ Where().Count() (baseline)")]
    public int LinqOptimization_WhereCount_Baseline()
    {
        return _testStrings.Where(s => s.StartsWith("test")).Count();
    }

    [Benchmark(Description = "LINQ Count(predicate)")]
    public int LinqOptimization_CountPredicate()
    {
        return _testStrings.Count(s => s.StartsWith("test"));
    }

    #endregion

    #region Span-based Parsing Benchmarks

    private readonly string _coordinateString = "123.456";

    [Benchmark(Baseline = true, Description = "String-based double parsing (baseline)")]
    public double SpanParsing_StringBased()
    {
        return double.Parse(_coordinateString);
    }

    [Benchmark(Description = "Span-based double parsing")]
    public double SpanParsing_SpanBased()
    {
        return SpanExtensions.TryParseDouble(_coordinateString.AsSpan(), out var result) ? result : 0;
    }

    #endregion

    #region Endianness Conversion Benchmarks

    [Benchmark(Baseline = true, Description = "Array.Reverse for endianness (baseline)")]
    public void EndiannessConversion_ArrayReverse()
    {
        var buffer = (byte[])_testData.Clone();
        for (int i = 0; i < buffer.Length; i += 4)
        {
            Array.Reverse(buffer, i, 4);
        }
    }

    [Benchmark(Description = "Span-based endianness conversion")]
    public void EndiannessConversion_Span()
    {
        var buffer = (byte[])_testData.Clone();
        SpanExtensions.ReverseEndianness(buffer.AsSpan(), 4);
    }

    #endregion

    #region Lambda Allocation Benchmarks

    [Benchmark(Baseline = true, Description = "Non-static lambda (baseline)")]
    public List<string> LambdaAllocation_NonStatic()
    {
        var prefix = "test";
        return _testStrings.Where(s => s.StartsWith(prefix)).ToList();
    }

    [Benchmark(Description = "Static lambda")]
    public List<string> LambdaAllocation_Static()
    {
        return _testStrings.Where(static s => s.StartsWith("test")).ToList();
    }

    #endregion

    #region String Comparison Benchmarks

    private readonly string _testString1 = "esriGeometryPoint";
    private readonly string _testString2 = "esrigeometrypoint";

    [Benchmark(Baseline = true, Description = "String.Equals default (baseline)")]
    public bool StringComparison_Default()
    {
        return _testString1.Equals(_testString2, StringComparison.Ordinal);
    }

    [Benchmark(Description = "String.Equals OrdinalIgnoreCase")]
    public bool StringComparison_OrdinalIgnoreCase()
    {
        return _testString1.Equals(_testString2, StringComparison.OrdinalIgnoreCase);
    }

    [Benchmark(Description = "Span EqualsIgnoreCase")]
    public bool StringComparison_SpanIgnoreCase()
    {
        return _testString1.AsSpan().EqualsIgnoreCase(_testString2.AsSpan());
    }

    #endregion
}
