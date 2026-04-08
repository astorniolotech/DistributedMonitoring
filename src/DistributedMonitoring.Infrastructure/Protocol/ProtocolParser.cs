using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Infrastructure.Protocol;

/// <summary>
/// Parser for sensor node protocol messages
/// Format: $TYPE,NODEID,S1,S2,S3,S4,STATUS,FLAGS*CHECKSUM#
/// </summary>
public class ProtocolParser
{
    private const char StartMarker = '$';
    private const char EndMarker = '#';
    private const char FieldSeparator = ',';

    /// <summary>
    /// Parse a raw message string into a SensorMessage
    /// </summary>
    public SensorMessage? Parse(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            return null;

        // Basic format validation
        if (!rawMessage.StartsWith(StartMarker) || !rawMessage.EndsWith(EndMarker))
            return null;

        try
        {
            // Extract payload (between $ and #)
            var endOfPayload = rawMessage.LastIndexOf(EndMarker);
            var payloadStart = rawMessage.IndexOf(StartMarker);
            var payloadWithChecksum = rawMessage.Substring(payloadStart + 1, endOfPayload - payloadStart - 1);

            // Split into payload and checksum
            var checksumSeparator = payloadWithChecksum.LastIndexOf('*');
            if (checksumSeparator < 0)
                return null;

            var payload = payloadWithChecksum.Substring(0, checksumSeparator);
            var receivedChecksum = payloadWithChecksum.Substring(checksumSeparator + 1);

            // Validate checksum
            var calculatedChecksum = CalculateChecksum(payload);
            if (!string.Equals(receivedChecksum, calculatedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                // Checksum mismatch - invalid message
                return new SensorMessage
                {
                    RawContent = rawMessage,
                    IsValid = false,
                    ReceivedAt = DateTime.Now
                };
            }

            // Parse fields
            var fields = payload.Split(FieldSeparator);
            if (fields.Length < 8)
                return null;

            var message = new SensorMessage
            {
                RawContent = rawMessage,
                IsValid = true,
                ReceivedAt = DateTime.Now
            };

            // Parse message type
            if (int.TryParse(fields[0], out int type))
                message.Type = (MessageType)type;

            // Parse node ID
            if (int.TryParse(fields[1], out int nodeId))
                message.NodeId = nodeId;

            // Parse sensor values (fields 2-5)
            var values = new List<double>();
            for (int i = 2; i <= 5; i++)
            {
                if (double.TryParse(fields[i], out double value))
                    values.Add(value);
            }
            message.Values = values;

            return message;
        }
        catch
        {
            return new SensorMessage
            {
                RawContent = rawMessage,
                IsValid = false,
                ReceivedAt = DateTime.Now
            };
        }
    }

    /// <summary>
    /// Calculate checksum for a payload string
    /// </summary>
    public static string CalculateChecksum(string payload)
    {
        int sum = 0;
        foreach (char c in payload)
        {
            sum += (byte)c;
        }
        return (sum & 0xFF).ToString("X2");
    }

    /// <summary>
    /// Build a command message for sending to nodes
    /// </summary>
    public string BuildCommand(int nodeId, string command)
    {
        var payload = $"2,{nodeId},{command},0,0,0,0,CMD,0";
        var checksum = CalculateChecksum(payload);
        return $"{StartMarker}{payload}*{checksum}{EndMarker}";
    }

    /// <summary>
    /// Build initialization command for broadcast
    /// </summary>
    public string BuildInitBroadcast()
    {
        var payload = "2,0,INIT_NODO,0,0,0,0,CMD,0";
        var checksum = CalculateChecksum(payload);
        return $"{StartMarker}{payload}*{checksum}{EndMarker}";
    }

    /// <summary>
    /// Validate message format without full parsing
    /// </summary>
    public bool ValidateFormat(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        return message.StartsWith(StartMarker) && 
               message.EndsWith(EndMarker) &&
               message.Contains("*") &&
               message.Contains(FieldSeparator.ToString());
    }
}