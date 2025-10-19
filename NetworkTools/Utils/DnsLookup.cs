using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace IPGeoLocator.NetworkTools.Utils
{
    public class DnsRecord
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public int Ttl { get; set; }
    }

    public class DnsLookupResult
    {
        public string Domain { get; set; } = "";
        public List<DnsRecord> Records { get; set; } = new();
        public DateTime LookupTime { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "";
        public string Server { get; set; } = "";
    }

    public class DnsLookup
    {
        private readonly LookupClient _lookupClient;

        public DnsLookup()
        {
            // You can specify DNS servers, or it will use the system's default
            _lookupClient = new LookupClient();
        }

        public DnsLookup(IPAddress dnsServer)
        {
            _lookupClient = new LookupClient(dnsServer);
        }

        public async Task<DnsLookupResult> LookupAsync(
            string domain,
            QueryType queryType,
            CancellationToken cancellationToken = default)
        {
            var result = new DnsLookupResult
            {
                Domain = domain,
                Server = _lookupClient.NameServers.FirstOrDefault()?.ToString() ?? "System Default"
            };

            try
            {
                var response = await _lookupClient.QueryAsync(domain, queryType, QueryClass.IN, cancellationToken);

                if (response.HasError)
                {
                    result.Status = response.ErrorMessage;
                    return result;
                }

                foreach (var answerRecord in response.Answers)
                {
                    var record = new DnsRecord
                    {
                        Name = answerRecord.DomainName.Value,
                        Ttl = answerRecord.TimeToLive,
                        Type = answerRecord.RecordType.ToString().ToUpper()
                    };

                    switch (answerRecord)
                    {
                        case ARecord a:
                            record.Value = a.Address.ToString();
                            break;
                        case AaaaRecord aaaa:
                            record.Value = aaaa.Address.ToString();
                            break;
                        case CNameRecord cname:
                            record.Value = cname.CanonicalName;
                            break;
                        case MxRecord mx:
                            record.Value = $"Preference: {mx.Preference}, Exchange: {mx.Exchange}";
                            break;
                        case NsRecord ns:
                            record.Value = ns.NSDName;
                            break;
                        case PtrRecord ptr:
                            record.Value = ptr.PtrDomainName;
                            break;
                        case TxtRecord txt:
                            record.Value = string.Join(" ", txt.Text);
                            break;
                        case SoaRecord soa:
                            record.Value = $"MName: {soa.MName}, RName: {soa.RName}, Serial: {soa.Serial}";
                            break;
                        default:
                            record.Value = answerRecord.ToString();
                            break;
                    }
                    result.Records.Add(record);
                }

                if (result.Records.Any())
                {
                    result.Status = "Success";
                }
                else
                {
                    result.Status = "No records found.";
                }
            }
            catch (DnsResponseException ex)
            {
                result.Status = $"DNS query failed: {ex.Message}";
            }
            catch (Exception ex)
            {
                result.Status = $"An unexpected error occurred: {ex.Message}";
            }

            return result;
        }
    }
}