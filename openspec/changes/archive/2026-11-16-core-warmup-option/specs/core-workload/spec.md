# Spec Delta

## ADDED Requirements

### Requirement: Measured operation count
The measured phase SHALL run exactly operationcount operations in total across all threads, and only those operations SHALL appear in the reported statistics.

#### Scenario: Default run
- **WHEN** a workload runs with operationcount 1000 and no warm-up option
- **THEN** the reported statistics contain exactly 1000 operations and the database receives exactly 1000 workload operations after the initial load

### Requirement: No warm-up by default
When the warmup option is not given, the benchmark SHALL run no warm-up phase, matching original YCSB, so the database receives no operations other than the initial load and the measured operations.

#### Scenario: Warm-up omitted
- **WHEN** the run command is given without --warmup
- **THEN** the effective warm-up is 0 seconds and no unmeasured workload operations are sent to the database

### Requirement: Opt-in warm-up duration
The warmup option SHALL set a warm-up duration in whole seconds. When it is greater than 0, the benchmark SHALL run the same operation mix for up to that duration before the measured phase. Warm-up operations SHALL NOT appear in the reported statistics, SHALL be sent to the database in addition to operationcount, and MAY change data, including inserts that grow the dataset before measurement.

#### Scenario: Previous behaviour restored
- **WHEN** a workload runs with --warmup 3 and operationcount 1000
- **THEN** a warm-up of up to 3 seconds runs first, the reported statistics still contain exactly 1000 operations, and the database receives more than 1000 workload operations

#### Scenario: Warm-up inserts change the dataset
- **WHEN** workload D runs with a warm-up greater than 0
- **THEN** the database receives more inserts than the reported insert count, and all inserted keys, from warm-up and measurement, are unique

### Requirement: Warm-up validation
The configuration SHALL be rejected before the benchmark starts when warmup is negative.

#### Scenario: Negative warm-up
- **WHEN** --warmup is -1
- **THEN** validation fails with a message naming warmup and the benchmark does not run

#### Scenario: Zero and positive warm-up accepted
- **WHEN** --warmup is 0 or 3
- **THEN** the warmup rule passes
