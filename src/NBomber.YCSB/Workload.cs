using System.ComponentModel;

namespace NBomber.YCSB
{
    public enum Workload
    {
        [Description("Update Heavy")]
        A,

        [Description("Read Heavy")]
        B,

        [Description("Read Only")]
        C,

        [Description("Read Latest")]
        D,

        [Description("Short Range")]
        E
    }

    public static class WorkloadManager
    {
        public static (string, int)[] GetOperations(Workload workload)
        {
            return workload switch
            {
                Workload.A => [("insert", 50), ("read", 50)],
                Workload.B => [("read", 95), ("insert", 5)],
                Workload.C => [("read", 100)],
                Workload.D => [("insert", 5), ("read_latest", 95)],
                Workload.E => [("scan", 5), ("read", 95)],
                _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
            };
        }

        public static string GetDescription(this Workload workload)
        {
            var field = workload.GetType().GetField(workload.ToString());
            var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attr?.Description ?? workload.ToString();
        }
    }
}
