# Zarr Endianness Support Implementation

## Overview
This document describes the implementation of endianness (byte-order) support for Zarr arrays in the HttpZarrReader, addressing Issue P2 #32.

## Problem Statement
The Zarr dtype parsing in `HttpZarrReader` didn't handle endianness markers like `<f4` (little-endian float32) or `>f8` (big-endian float64), which could cause data corruption when reading Zarr arrays created on systems with different byte orders.

## Solution

### 1. Dtype Parsing Enhancement
Added comprehensive dtype parsing to extract both element size and endianness information:

```csharp
private (int elementSize, Endianness endianness) ParseDtype(string dtype)
```

**Supported Formats:**
- **Little-endian:** `<f4`, `<f8`, `<i2`, `<i4`, `<i8`, `<u1`, `<u2`, `<u4`, `<u8`
- **Big-endian:** `>f4`, `>f8`, `>i2`, `>i4`, `>i8`, `>u2`, `>u4`, `>u8`
- **Native/Not applicable:** `|u1`, `|i1`
- **Legacy (no prefix):** `f4`, `f8`, `i4`, etc. (defaults to little-endian)
- **Full names:** `float32`, `float64`, `int32`, etc.

### 2. Byte-Order Conversion
Added automatic byte-order conversion when reading chunks:

```csharp
private byte[] ConvertByteOrder(byte[] data, string dtype)
```

**Conversion Logic:**
1. Parse dtype to determine array endianness
2. Compare with system endianness (`BitConverter.IsLittleEndian`)
3. Only convert if endianness differs AND element size > 1 byte
4. Perform in-place byte reversal for efficiency

**Optimized Reversal:**
- 2-byte elements: Simple swap
- 4-byte elements: Two swaps
- 8-byte elements: Four swaps
- Other sizes: Generic `Array.Reverse()`

### 3. Integration Points
The byte-order conversion is applied automatically in:
- `FetchAndDecompressChunkAsync()` - After decompression, before returning
- Sparse array handling - Uses parsed dtype for correct chunk sizing

## Files Modified

### Source Files
- **src/Honua.Server.Core/Raster/Readers/HttpZarrReader.cs**
  - Added `using System.Buffers.Binary` for future optimizations
  - Added comprehensive XML documentation
  - Added `Endianness` enum (internal)
  - Replaced `GetElementSize()` with `ParseDtype()` method
  - Added `GetElementSizeFromTypeChar()` helper method
  - Added `ConvertByteOrder()` method
  - Added `ReverseBytes()` optimized helper method
  - Updated `FetchAndDecompressChunkAsync()` to apply conversion
  - Updated `AssembleSlice()` to use new dtype parsing

### Test Files
- **tests/Honua.Server.Core.Tests/Raster/Readers/HttpZarrReaderEndiannessTests.cs** (NEW)
  - 15+ comprehensive test cases covering:
    - Dtype parsing for all endianness markers
    - Little-endian, big-endian, and native byte order
    - Legacy format support
    - Single and multiple element conversion
    - All numeric types (float32, float64, int16, int32, int64)
    - Edge cases (sparse arrays, single-byte types)

### Verification Test
- **tests/EndianessTestRunner/** (Standalone console app)
  - Validates core logic independently
  - All tests pass on little-endian systems

## Supported Dtype Formats

| Prefix | Meaning | Example | Description |
|--------|---------|---------|-------------|
| `<` | Little-endian | `<f4` | Least significant byte first (Intel/AMD) |
| `>` | Big-endian | `>f8` | Most significant byte first (network order) |
| `|` | Not applicable | `|u1` | Single byte or native order |
| (none) | Legacy | `f4` | Defaults to little-endian |

| Type Code | Size | Description |
|-----------|------|-------------|
| `f4` | 4 bytes | 32-bit floating point |
| `f8` | 8 bytes | 64-bit floating point |
| `i1` | 1 byte | 8-bit signed integer |
| `i2` | 2 bytes | 16-bit signed integer |
| `i4` | 4 bytes | 32-bit signed integer |
| `i8` | 8 bytes | 64-bit signed integer |
| `u1` | 1 byte | 8-bit unsigned integer |
| `u2` | 2 bytes | 16-bit unsigned integer |
| `u4` | 4 bytes | 32-bit unsigned integer |
| `u8` | 8 bytes | 64-bit unsigned integer |

## Example Usage

### Reading Little-Endian Array
```json
// .zarray metadata
{
  "dtype": "<f4",
  "shape": [100, 100],
  "chunks": [10, 10]
}
```
- On little-endian system: No conversion needed
- On big-endian system: Bytes reversed automatically

### Reading Big-Endian Array
```json
// .zarray metadata
{
  "dtype": ">f8",
  "shape": [50, 50],
  "chunks": [5, 5]
}
```
- On little-endian system: Bytes reversed automatically
- On big-endian system: No conversion needed

## Performance Considerations

1. **Zero-Copy When Possible:** Conversion only applied when necessary
2. **In-Place Conversion:** Reuses existing byte array
3. **Optimized Swapping:** Switch-case for common sizes (2, 4, 8 bytes)
4. **Single-Pass:** All elements converted in one iteration

## Edge Cases Handled

1. **Single-byte types** (`u1`, `i1`): No conversion needed
2. **Sparse arrays**: Empty chunks return zero-filled arrays with correct size
3. **Legacy formats**: Default to little-endian for backward compatibility
4. **Invalid dtypes**: Throw descriptive `ArgumentException`

## Testing

### Unit Tests (xUnit)
- 15+ test cases covering all scenarios
- Mocked HTTP responses for isolated testing
- Tests for all numeric types and endianness combinations

### Verification Tests
- Standalone console app validates core logic
- Tests pass on standard little-endian systems (x86/x64)

### Test Results
```
Test 1: Dtype Parsing
  ✓ All dtype parsing tests passed

Test 2: Byte Order Conversion
  ✓ All byte order conversion tests passed

Test 3: Multiple Element Conversion
  Float values: 3.14159, 2.71828, 1
  ✓ All multiple element tests passed

=== All Tests Passed ===
```

## Future Enhancements

1. **BinaryPrimitives Optimization:**
   - Use `BinaryPrimitives.ReverseEndianness()` for modern .NET
   - Hardware-accelerated on supported platforms

2. **SIMD Optimization:**
   - Use `System.Runtime.Intrinsics` for bulk conversion
   - Significant speedup for large arrays

3. **Complex Type Support:**
   - Support for complex64/complex128 dtypes
   - Structured dtypes with multiple fields

4. **Endianness Detection:**
   - Auto-detect endianness from first chunk if not specified
   - Useful for legacy datasets

## References

- Zarr Format Specification: https://zarr.readthedocs.io/
- NumPy dtype strings: https://numpy.org/doc/stable/reference/arrays.dtypes.html
- BitConverter class: https://learn.microsoft.com/en-us/dotnet/api/system.bitconverter

## Related Issues

- P2 #32: Blosc Endianness Support for Zarr arrays

## Author

Implementation Date: 2025-10-18
