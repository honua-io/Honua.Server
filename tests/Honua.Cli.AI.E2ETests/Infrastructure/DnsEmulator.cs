using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Minimal in-memory DNS emulator that tracks CNAME records per zone.
/// </summary>
internal sealed class DnsEmulator
{
    private readonly Dictionary<string, Dictionary<string, string>> _zones = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterZone(string zoneName)
    {
        zoneName = Normalize(zoneName);
        if (!_zones.ContainsKey(zoneName))
        {
            _zones[zoneName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public void UpsertCname(string zoneName, string recordName, string endpoint)
    {
        zoneName = Normalize(zoneName);
        recordName = NormalizeRecord(recordName);
        endpoint = Normalize(endpoint);

        if (!_zones.TryGetValue(zoneName, out var records))
        {
            throw new InvalidOperationException($"Zone '{zoneName}' is not registered.");
        }

        records[recordName] = endpoint;
    }

    public bool DeleteCname(string zoneName, string recordName)
    {
        zoneName = Normalize(zoneName);
        recordName = NormalizeRecord(recordName);

        if (!_zones.TryGetValue(zoneName, out var records))
        {
            return false;
        }

        return records.Remove(recordName);
    }

    public bool TryGetCname(string zoneName, string recordName, out string endpoint)
    {
        zoneName = Normalize(zoneName);
        recordName = NormalizeRecord(recordName);

        if (_zones.TryGetValue(zoneName, out var records) &&
            records.TryGetValue(recordName, out var value))
        {
            endpoint = value;
            return true;
        }

        endpoint = string.Empty;
        return false;
    }

    public bool TryGetCnameByDnsName(string dnsName, out string endpoint)
    {
        dnsName = Normalize(dnsName);

        foreach (var zone in _zones.Keys.OrderByDescending(z => z.Length))
        {
            if (dnsName.EndsWith(zone, StringComparison.OrdinalIgnoreCase))
            {
                var recordName = GetRecordSegment(dnsName, zone);
                return TryGetCname(zone, recordName, out endpoint);
            }
        }

        endpoint = string.Empty;
        return false;
    }

    public IReadOnlyDictionary<string, string> GetZoneRecords(string zoneName)
    {
        zoneName = Normalize(zoneName);

        if (_zones.TryGetValue(zoneName, out var records))
        {
            return new Dictionary<string, string>(records, StringComparer.OrdinalIgnoreCase);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetRecordSegment(string dnsName, string zoneName)
    {
        var trimmedDns = Normalize(dnsName);
        var trimmedZone = Normalize(zoneName);

        if (trimmedDns.Equals(trimmedZone, StringComparison.OrdinalIgnoreCase))
        {
            return "@";
        }

        if (trimmedDns.EndsWith(trimmedZone, StringComparison.OrdinalIgnoreCase))
        {
            var prefix = trimmedDns[..^(trimmedZone.Length)];
            prefix = prefix.TrimEnd('.');
            return string.IsNullOrEmpty(prefix) ? "@" : prefix;
        }

        return trimmedDns;
    }

    private static string Normalize(string value) => value.Trim().TrimEnd('.');

    private static string NormalizeRecord(string record) =>
        record.Equals("@", StringComparison.Ordinal) ? "@" : Normalize(record);
}
