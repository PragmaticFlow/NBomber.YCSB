# Spec Delta

## ADDED Requirements

### Requirement: Reads target only acknowledged inserts
Read, read latest, update, scan and read-modify-write operations SHALL choose keys only up to the acknowledged key limit: the highest key such that it and every key below it are either part of the initial load or acknowledged inserts. A key reserved by an insert that has not been acknowledged SHALL NOT be chosen.

#### Scenario: Key reserved but not acknowledged
- **WHEN** the initial load has keys 1 to 1000 and an insert has reserved key 1001 but not yet acknowledged it
- **THEN** read latest, read, update, scan and read-modify-write never choose key 1001

#### Scenario: Newest acknowledged key is reachable by read latest
- **WHEN** inserts for keys 1001 and 1002 are both acknowledged
- **THEN** read latest can choose key 1002 and never chooses a key above 1002

### Requirement: Insert key acknowledgement
Each insert SHALL reserve a new key before its measured database call and acknowledge that key after the call completes, whether the insert succeeded or failed, matching original YCSB.

#### Scenario: Successful insert
- **WHEN** an insert of key 1001 succeeds
- **THEN** key 1001 is acknowledged

#### Scenario: Failed insert
- **WHEN** an insert of key 1001 returns a failure or throws
- **THEN** key 1001 is still acknowledged, so later inserts can advance the limit past it

### Requirement: Acknowledged limit advances over contiguous keys
The acknowledged key limit SHALL advance only over a contiguous run of acknowledged keys starting right after the current limit; a key acknowledged out of order SHALL become readable only when every lower reserved key is acknowledged.

#### Scenario: Out-of-order acknowledgement
- **WHEN** keys 1001, 1002 and 1003 are reserved after an initial load of 1000 records, and 1003 is acknowledged first
- **THEN** the limit stays 1000; after 1001 is acknowledged it is 1001; after 1002 is acknowledged it is 1003

#### Scenario: No inserts
- **WHEN** a workload without inserts runs after an initial load of 1000 records
- **THEN** the limit is 1000 for the whole run

### Requirement: Unique insert keys under concurrency
Concurrent inserts SHALL reserve distinct, consecutive keys starting right after recordcount, and concurrent acknowledgements SHALL leave the limit equal to the highest reserved key once all are acknowledged.

#### Scenario: Parallel reservation and acknowledgement
- **WHEN** 10000 inserts reserve and acknowledge keys in parallel after an initial load of 1000 records
- **THEN** the reserved keys are exactly 1001 to 11000, each once, and the limit ends at 11000
