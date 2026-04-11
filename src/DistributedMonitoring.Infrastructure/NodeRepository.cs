using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Infrastructure;

/// <summary>
/// In-memory repository for node state management
/// </summary>
public class NodeRepository : INodeRepository
{
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly IConfigurationService _configService;
    private readonly object _lock = new();

    public NodeRepository(IConfigurationService configService)
    {
        _configService = configService;
        InitializeNodes();
    }

    private void InitializeNodes()
    {
        var config = _configService.GetConfiguration();

        foreach (var nodeConfig in config.Nodes)
        {
            var node = new Node
            {
                Id = nodeConfig.Id,
                Name = nodeConfig.Name,
                IsEnabled = nodeConfig.Enabled,
                Status = NodeStatus.Unknown,
                Sensors = nodeConfig.Sensors.Select(s => new Sensor
                {
                    Id = s.Id,
                    Name = s.Name,
                    Unit = s.Unit,
                    IsEnabled = true,
                    Limits = s.Limits
                }).ToList()
            };

            _nodes[node.Id] = node;
        }
    }

    public Node? GetNode(int id)
    {
        lock (_lock)
        {
            return _nodes.TryGetValue(id, out var node) ? node : null;
        }
    }

    public IEnumerable<Node> GetAllNodes()
    {
        lock (_lock)
        {
            return _nodes.Values.ToList(); // Return a copy
        }
    }

    public void UpdateNode(Node node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        lock (_lock)
        {
            _nodes[node.Id] = node;
        }
    }

    /// <summary>
    /// Add a new node to the repository
    /// </summary>
    public void AddNode(Node node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        lock (_lock)
        {
            if (!_nodes.ContainsKey(node.Id))
            {
                _nodes[node.Id] = node;
            }
        }
    }

    /// <summary>
    /// Remove a node from the repository
    /// </summary>
    public void RemoveNode(int id)
    {
        lock (_lock)
        {
            _nodes.Remove(id);
        }
    }

    /// <summary>
    /// Clear all nodes
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _nodes.Clear();
        }
    }
}
