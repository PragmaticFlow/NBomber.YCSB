The **Yahoo! Cloud Serving Benchmark (YCSB)** is an open-source database benchmarking suite and a critical analytical component of cloud-based database management system (DBMS) evaluation. It allows users to comparatively measure how various modern SQL and NoSQL DBMS perform simple database operations on generated datasets.

The original benchmark was developed by workers in the research division of Yahoo! who released it in 2010 with the stated goal of "facilitating performance comparisons of the new generation of cloud data serving systems", particularly for transaction-processing workloads which differed from ones measured by benchmarks designed for more traditional database management systems.

The strength of YCSB benchmarks is that they can be used to compare databases that are architecturally distinct and measure the way various database configurations perform under different workloads.

The YCSB framework also automates or simplifies essential benchmarking process tasks such as:
- Defining core workloads with essential parameters
- Connecting to the database with the database drivers
- Executing the workload on the database
- Collecting and storing performance data
- Creating a new interface layer for benchmarking

## Warm-up

By default there is no warm-up, as in YCSB: the database receives exactly `operationcount` operations, all measured. Use `--warmup <seconds>` to add a warm-up before measurement. Warm-up operations are extra, are not measured and change data, and because warm-up is time-capped a faster database runs more of them.

Results produced before this option existed used a fixed 3-second warm-up and may not be directly comparable. `--warmup 3` reproduces that behaviour.

## Links

- [Our project docs](https://nbomber.com/docs/load-testing-databases/overview)
