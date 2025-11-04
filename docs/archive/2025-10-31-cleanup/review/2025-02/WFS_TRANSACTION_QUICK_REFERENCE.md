# WFS Transaction Streaming - Quick Reference

Quick guide for using and configuring WFS Transaction streaming features.

---

## Configuration

### appsettings.json

```json
{
  "honua:wfs": {
    "MaxTransactionFeatures": 5000,
    "TransactionBatchSize": 500,
    "TransactionTimeoutSeconds": 300,
    "EnableStreamingTransactionParser": true
  }
}
```

### Options Explained

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| `MaxTransactionFeatures` | 5000 | 1-100,000 | Maximum features in a single transaction |
| `TransactionBatchSize` | 500 | 10-10,000 | Batch size for processing operations |
| `TransactionTimeoutSeconds` | 300 | 10-3,600 | Timeout for transaction execution |
| `EnableStreamingTransactionParser` | true | true/false | Use streaming XML parser |

---

## Performance Guidelines

### Small Transactions (< 100 features)
- Use default settings
- Both parsers perform similarly
- Memory usage negligible

### Medium Transactions (100-1,000 features)
- Use streaming parser (default)
- Expected memory: ~300 KB - 1.5 MB
- Processing time: 700ms - 1s

### Large Transactions (1,000-5,000 features)
- Use streaming parser (required)
- Expected memory: ~1.5 MB - 3 MB
- Processing time: 3-5 seconds
- Consider increasing timeout

### Very Large Transactions (> 5,000 features)
- Increase `MaxTransactionFeatures` limit
- Increase `TransactionTimeoutSeconds`
- Monitor memory usage
- Consider batching at client side

---

## Troubleshooting

### Transaction Exceeds Limit

**Error**: "Transaction contains X operations, exceeding the maximum of Y"

**Solution**:
```json
{
  "honua:wfs": {
    "MaxTransactionFeatures": 10000
  }
}
```

### Transaction Timeout

**Error**: "OperationCanceledException"

**Solution**:
```json
{
  "honua:wfs": {
    "TransactionTimeoutSeconds": 600
  }
}
```

### Memory Pressure

**Symptom**: High memory usage, GC pressure, slow performance

**Solution 1**: Verify streaming is enabled
```json
{
  "honua:wfs": {
    "EnableStreamingTransactionParser": true
  }
}
```

**Solution 2**: Reduce batch size
```json
{
  "honua:wfs": {
    "TransactionBatchSize": 250
  }
}
```

### Rollback to Legacy Parser

If streaming parser causes issues:
```json
{
  "honua:wfs": {
    "EnableStreamingTransactionParser": false
  }
}
```

---

## Example Transactions

### Small Insert (1 feature)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:Transaction xmlns:wfs="http://www.opengis.net/wfs/2.0"
                 xmlns:parks="http://honua.io/service/parks"
                 service="WFS" version="2.0.0">
  <wfs:Insert>
    <parks:park>
      <parks:id>101</parks:id>
      <parks:name>Central Park</parks:name>
      <parks:area>341</parks:area>
    </parks:park>
  </wfs:Insert>
</wfs:Transaction>
```

### Bulk Insert (Multiple features)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:Transaction xmlns:wfs="http://www.opengis.net/wfs/2.0"
                 xmlns:parks="http://honua.io/service/parks"
                 service="WFS" version="2.0.0">
  <wfs:Insert>
    <parks:park><parks:id>101</parks:id><parks:name>Park 1</parks:name></parks:park>
    <parks:park><parks:id>102</parks:id><parks:name>Park 2</parks:name></parks:park>
    <parks:park><parks:id>103</parks:id><parks:name>Park 3</parks:name></parks:park>
  </wfs:Insert>
</wfs:Transaction>
```

### Mixed Operations

```xml
<?xml version="1.0" encoding="UTF-8"?>
<wfs:Transaction xmlns:wfs="http://www.opengis.net/wfs/2.0"
                 xmlns:fes="http://www.opengis.net/fes/2.0"
                 xmlns:parks="http://honua.io/service/parks"
                 service="WFS" version="2.0.0">
  <!-- Insert new feature -->
  <wfs:Insert>
    <parks:park>
      <parks:id>104</parks:id>
      <parks:name>New Park</parks:name>
    </parks:park>
  </wfs:Insert>

  <!-- Update existing feature -->
  <wfs:Update typeName="parks:park">
    <wfs:Property>
      <wfs:Name>name</wfs:Name>
      <wfs:Value>Updated Park Name</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:ResourceId rid="parks:park.101"/>
    </fes:Filter>
  </wfs:Update>

  <!-- Delete feature -->
  <wfs:Delete typeName="parks:park">
    <fes:Filter>
      <fes:ResourceId rid="parks:park.102"/>
    </fes:Filter>
  </wfs:Delete>
</wfs:Transaction>
```

---

## Monitoring

### Metrics to Watch

- `honua.wfs.transaction.duration` - Transaction processing time
- `honua.wfs.transaction.feature_count` - Features per transaction
- `honua.wfs.transaction.memory_peak` - Peak memory usage
- `honua.wfs.transaction.failures` - Transaction failure rate

### Grafana Query Examples

```promql
# Average transaction processing time
rate(honua_wfs_transaction_duration_sum[5m])
/ rate(honua_wfs_transaction_duration_count[5m])

# 99th percentile memory usage
histogram_quantile(0.99, honua_wfs_transaction_memory_peak)

# Transaction failure rate
rate(honua_wfs_transaction_failures_total[5m])
```

---

## Testing

### Unit Tests

```bash
# Run streaming parser tests
dotnet test --filter "FullyQualifiedName~WfsTransactionStreaming"

# Run memory tests
dotnet test --filter "FullyQualifiedName~WfsTransactionMemory"
```

### Load Testing

```bash
# Generate test transaction with 1000 features
./scripts/generate-test-transaction.sh 1000 > transaction.xml

# Execute transaction
curl -X POST \
  -H "Content-Type: application/xml" \
  -H "Authorization: Bearer $TOKEN" \
  --data-binary @transaction.xml \
  "https://api.example.com/wfs?service=WFS&request=Transaction"
```

### Memory Profiling

```bash
# Profile memory usage during transaction
dotnet-counters monitor --process-id $PID \
  --counters System.Runtime[gc-heap-size,gc-committed,alloc-rate]
```

---

## Best Practices

### Client-Side

1. **Batch Size**: Keep transactions under 1,000 features when possible
2. **Timeouts**: Set client timeout > server timeout + 30s
3. **Retries**: Implement exponential backoff for failures
4. **Validation**: Validate XML before sending
5. **Compression**: Use gzip Content-Encoding for large payloads

### Server-Side

1. **Monitoring**: Track transaction metrics continuously
2. **Limits**: Start with conservative limits, increase as needed
3. **Streaming**: Keep streaming parser enabled
4. **Resources**: Allocate sufficient memory for peak load
5. **Testing**: Load test with production-like transaction sizes

---

## Migration Checklist

### Pre-Deployment

- [ ] Review current transaction patterns
- [ ] Determine appropriate limits
- [ ] Configure `appsettings.json`
- [ ] Run unit tests
- [ ] Run integration tests
- [ ] Performance test with realistic workloads

### Deployment

- [ ] Deploy with streaming enabled (default)
- [ ] Monitor memory usage
- [ ] Monitor transaction durations
- [ ] Check error logs
- [ ] Verify OGC compliance

### Post-Deployment

- [ ] Analyze metrics for 24-48 hours
- [ ] Adjust limits if needed
- [ ] Document any issues
- [ ] Update runbooks
- [ ] Train operations team

---

## Support

### Logs

Transaction failures are logged with context:
```
2025-10-29 10:15:23 [ERR] WFS Transaction failed: Transaction contains 6000 operations, exceeding the maximum of 5000
```

### Common Issues

1. **"Transaction payload too large"**: Increase `SecureXmlSettings.MaxInputStreamSize`
2. **"releaseAction must be ALL or SOME"**: Fix XML attribute value
3. **"Transaction operations require DataPublisher role"**: Check user permissions
4. **"Locked features are not available"**: Acquire lock or provide valid lockId

---

## See Also

- [WFS_XML_STREAMING_FIX_COMPLETE.md](./WFS_XML_STREAMING_FIX_COMPLETE.md) - Complete documentation
- [wfs-wms.md](./wfs-wms.md) - WFS & WMS review
- [crosscut-performance.md](./crosscut-performance.md) - Performance analysis
- [OGC WFS 2.0 Specification](http://docs.opengeospatial.org/is/09-025r2/09-025r2.html)
