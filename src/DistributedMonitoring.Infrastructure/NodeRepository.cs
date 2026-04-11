using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Infrastructure;

/// <summary>
/// In-memory repository for node state management
/// </summary>
public class NodeRepository : INodeRepository
{
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly object _lock = new();

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
