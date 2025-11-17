using NBomber.Contracts;

namespace NBomber.YCSB.DAL
{
    public interface IDbYcsbClient
    {
        Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values);
        Task<Response<object>> Update(string table, string key, Dictionary<string, string> values);
        Task<Response<object>> Read(string table, string key, HashSet<string> columns);
        Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string> columns);
        Task<Response<object>> DeleteAllData();
        Task<Response<object>> InitDb();
        Task<Response<object>> BulkInsert(Dictionary<string, Dictionary<string, string>> data);
    }
}
