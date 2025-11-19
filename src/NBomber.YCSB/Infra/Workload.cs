namespace NBomber.YCSB.Infra
{
    public enum Workload
    {
        A, //Update Heavy
        B, //Read Heavy
        C, //Read Only
        //D, //Read Latest
        E, //Short Range
    }

    public static class WorkloadManager
    {
        public static (string, int)[] GetOperations(Workload workload)
        {
            return workload switch
            {
                Workload.A => [("insert", 50), ("read", 50)],
                Workload.B => [("read", 95), ("update", 5)],
                Workload.C => [("read", 100)],
                //Workload.D => [("read_lates", 95), ("insert", 5)],
                Workload.E => [("scan", 95), ("insert", 5)],
                _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
            };
        }

        public static string GetDescription(Workload workload)
        {
            return workload switch
            {
                Workload.A => "Update Heavy",
                Workload.B => "Read Heavy",
                Workload.C => "Read Only",
                //Workload.D => "Read Latest",
                Workload.E => "Short Range",
                _ => ""
            };
        }
    }
}
