using NBomber.Contracts;
using NBomber.CSharp;

namespace NBomber.YCSB.DAL
{
    //public enum Status // замінить на Response і враховувати байтиб подивиться як передавати size
    //{
    //    Ok,
    //    Error
    //}

    public interface IDbYcsbClient
    {
        Task<Response<object>> Insert(string key, Dictionary<string, string> values);
        Task<Response<object>> Update(string key, Dictionary<string, string> values);
        Task<Response<object>> Read(string key);
        Task<Response<object>> ReadLatest();
        Task<Response<object>> Scan(string startKey, int count);
        Task<Response<object>> DeleteAllData();
        Task<Response<object>> InitDb();
        Task<Response<object>> BulkInsert(Dictionary<string, Dictionary<string, string>> data);
    }
}
