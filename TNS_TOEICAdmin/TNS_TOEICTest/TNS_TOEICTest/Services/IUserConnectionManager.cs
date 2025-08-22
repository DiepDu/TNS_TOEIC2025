
namespace TNS_TOEICTest.Services
{
    public interface IUserConnectionManager
    {
        void AddConnection(string userKey, string connectionId);
        void RemoveConnection(string connectionId);
        HashSet<string> GetConnectionIds(string userKey);
    }
}