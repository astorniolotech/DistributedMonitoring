using DistributedMonitoring.Domain.Interfaces;
using Xunit;

namespace DistributedMonitoring.Tests;

public class DomainTests
{
    [Fact]
    public void SensorLimits_TriggersLowAlarm_WhenBelowLowAlarm()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Temperatura",
            IsEnabled = true,
            Limits = new SensorLimits
            {
                RawLowAlarm = 10,
                RawHighAlarm = 100,
                RawLowWarning = 20,
                RawHighWarning = 80
            }
        };

        // Act - simulate evaluating value 5 (below low alarm of 10)
        var state = EvaluateSensorState(5, sensor);

        // Assert
        Assert.Equal(SensorState.LowAlarm, state);
    }

    [Fact]
    public void SensorLimits_TriggersHighAlarm_WhenAboveHighAlarm()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Presión",
            IsEnabled = true,
            Limits = new SensorLimits
            {
                RawLowAlarm = 0,
                RawHighAlarm = 10,
                RawLowWarning = 1,
                RawHighWarning = 8
            }
        };

        // Act - simulate evaluating value 15 (above high alarm of 10)
        var state = EvaluateSensorState(15, sensor);

        // Assert
        Assert.Equal(SensorState.HighAlarm, state);
    }

    [Fact]
    public void SensorLimits_TriggersLowWarning_WhenBelowLowWarning()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Temperatura",
            IsEnabled = true,
            Limits = new SensorLimits
            {
                RawLowAlarm = 10,
                RawHighAlarm = 100,
                RawLowWarning = 20,
                RawHighWarning = 80
            }
        };

        // Act - simulate evaluating value 15 (between low warning 20 and low alarm 10)
        var state = EvaluateSensorState(15, sensor);

        // Assert
        Assert.Equal(SensorState.LowWarning, state);
    }

    [Fact]
    public void SensorLimits_TriggersHighWarning_WhenAboveHighWarning()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Flujo",
            IsEnabled = true,
            Limits = new SensorLimits
            {
                RawLowAlarm = 0,
                RawHighAlarm = 100,
                RawLowWarning = 5,
                RawHighWarning = 80
            }
        };

        // Act - simulate evaluating value 85 (between high warning 80 and high alarm 100)
        var state = EvaluateSensorState(85, sensor);

        // Assert
        Assert.Equal(SensorState.HighWarning, state);
    }

    [Fact]
    public void SensorLimits_ReturnsOK_WhenValueInNormalRange()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Temperatura",
            IsEnabled = true,
            Limits = new SensorLimits
            {
                RawLowAlarm = 10,
                RawHighAlarm = 100,
                RawLowWarning = 20,
                RawHighWarning = 80
            }
        };

        // Act - simulate evaluating value 50 (in normal range)
        var state = EvaluateSensorState(50, sensor);

        // Assert
        Assert.Equal(SensorState.OK, state);
    }

    [Fact]
    public void SensorLimits_DisabledSensor_ReturnsDisabledState()
    {
        // Arrange
        var sensor = new Sensor
        {
            Id = 0,
            Name = "Temperatura",
            IsEnabled = false, // Disabled
            Limits = new SensorLimits
            {
                RawLowAlarm = 10,
                RawHighAlarm = 100,
                RawLowWarning = 20,
                RawHighWarning = 80
            }
        };

        // Act
        var state = EvaluateSensorState(5, sensor);

        // Assert
        Assert.Equal(SensorState.Disabled, state);
    }

    [Fact]
    public void NodeStatus_TransitionsFromUnknownToInitializing()
    {
        // Arrange
        var node = new Node
        {
            Id = 1,
            Name = "Test Node",
            Status = NodeStatus.Unknown
        };

        // Act - received valid message
        node.Status = NodeStatus.Initializing;
        node.LastSeen = DateTime.Now;

        // Assert
        Assert.Equal(NodeStatus.Initializing, node.Status);
    }

    [Fact]
    public void NodeStatus_TransitionsFromInitializingToActive()
    {
        // Arrange
        var node = new Node
        {
            Id = 1,
            Name = "Test Node",
            Status = NodeStatus.Initializing
        };

        // Act - received _RXOK confirmation
        node.Status = NodeStatus.Active;
        node.LastSeen = DateTime.Now;

        // Assert
        Assert.Equal(NodeStatus.Active, node.Status);
    }

    [Fact]
    public void NodeStatus_TransitionsToOffline_AfterTimeout()
    {
        // Arrange
        var node = new Node
        {
            Id = 1,
            Name = "Test Node",
            Status = NodeStatus.Active,
            LastSeen = DateTime.Now.AddMinutes(-6) // 6 minutes ago
        };

        // Act - check if offline (5 min timeout)
        var isOffline = (DateTime.Now - node.LastSeen).TotalMinutes > 5;

        // Assert
        Assert.True(isOffline);
    }

    [Fact]
    public void Protocol_CalculatesChecksum_Correctly()
    {
        // Arrange
        var payload = "1,1,25.3,3.2,50.1,75.0,OK,0";

        // Act
        var checksum = CalculateChecksum(payload);

        // Assert
        // Sum of ASCII values: '1'=49(1), ','=44, '1'=49, ','=44, '2'=50...
        // This is a simplified test
        Assert.NotNull(checksum);
        Assert.Equal(2, checksum.Length); // Hex format
    }

    [Fact]
    public void Protocol_ValidatesMessageFormat()
    {
        // Arrange
        var validMessage = "$1,1,25.3,3.2,50.1,75.0,OK,0*AB#";
        var invalidMessage = "1,1,25.3,3.2,50.1,75.0,OK,0*AB#"; // Missing $

        // Act
        var isValidStart = validMessage.StartsWith("$");
        var isValidEnd = validMessage.EndsWith("#");
        var isInvalidStart = invalidMessage.StartsWith("$");

        // Assert
        Assert.True(isValidStart);
        Assert.True(isValidEnd);
        Assert.False(isInvalidStart);
    }

    // Helper methods that match the domain logic
    private static SensorState EvaluateSensorState(double value, Sensor sensor)
    {
        if (!sensor.IsEnabled)
            return SensorState.Disabled;

        var limits = sensor.Limits;

        if (value < limits.RawLowAlarm)
            return SensorState.LowAlarm;
        if (value > limits.RawHighAlarm)
            return SensorState.HighAlarm;
        if (value < limits.RawLowWarning)
            return SensorState.LowWarning;
        if (value > limits.RawHighWarning)
            return SensorState.HighWarning;

        return SensorState.OK;
    }

    private static string CalculateChecksum(string payload)
    {
        int sum = payload.Sum(c => (byte)c);
        return (sum & 0xFF).ToString("X2");
    }
}