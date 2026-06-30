using CommandLine;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Cluster;

/// <summary>
/// Command line options for running the YCSB benchmark as an NBomber Cluster node
/// (https://nbomber.com/docs/cluster/overview).
///
/// Inherits all YCSB workload options from <see cref="YcsbCliArgs"/> (workload, recordcount,
/// db, props, ...) and adds the cluster-specific options needed to start a node as a
/// coordinator or an agent.
/// </summary>
[Verb("cluster", HelpText = "Run the YCSB benchmark as an NBomber Cluster node (coordinator or agent).")]
public class ClusterCliArgs : YcsbCliArgs
{
    [Option("config", Required = false, Default = "Cluster/auto-cluster-config.json",
        HelpText = "Path to the NBomber cluster config (auto-cluster-config.json or manual-cluster-config.json).")]
    public string Config { get; set; } = "Cluster/auto-cluster-config.json";

    [Option("agentscount", Required = false, Default = 0,
        HelpText = "Number of agents the coordinator waits for before starting (0 = use value from config).")]
    public int AgentsCount { get; set; }

    [Option("localdev", Required = false, Default = true,
        HelpText = "Enable the license-free local dev cluster (Coordinator + 1 Agent). Set false when using an Enterprise license.")]
    public bool LocalDev { get; set; }

    [Option("license", Required = false,
        HelpText = "NBomber Enterprise license key. Required to run more than one agent (i.e. disable --localdev).")]
    public string? License { get; set; }
}
