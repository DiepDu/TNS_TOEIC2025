
using System.Collections.Concurrent;

namespace TNS_TOEICTest.Services
{
    public class UserConnectionManager : IUserConnectionManager
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

        public void AddConnection(string userKey, string connectionId)
        {
            var connections = _userConnections.GetOrAdd(userKey, k => new HashSet<string>());
            lock (connections)
            {
                connections.Add(connectionId);
            }
        }

        public void RemoveConnection(string connectionId)
        {
            foreach (var userKey in _userConnections.Keys)
            {
                if (_userConnections.TryGetValue(userKey, out var connections))
                {
                    lock (connections)
                    {
                        if (connections.Contains(connectionId))
                        {
                            connections.Remove(connectionId);
                            if (connections.Count == 0)
                            {
                                _userConnections.TryRemove(userKey, out _);
                            }
                            break;
                        }
                    }
                }
            }
        }

        public HashSet<string> GetConnectionIds(string userKey)
        {
            _userConnections.TryGetValue(userKey, out var connections);
            return connections ?? new HashSet<string>();
        }
    }
}