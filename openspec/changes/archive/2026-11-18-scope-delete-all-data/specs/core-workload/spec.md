## ADDED Requirements

### Requirement: Scenario initialisation order
Before any measured operation, the scenario SHALL call the database client's delete-all-data, then its initialisation, then bulk-load the configured records, each once and in this order. Delete-all-data SHALL succeed on a database never used by the benchmark, and initialisation SHALL create everything the load and operations need even when delete-all-data removed the benchmark table, keyspace objects or database. The reset scope of each client is defined by that client's specification.

#### Scenario: Regular run
- **WHEN** a workload starts on a database that holds data from an earlier run
- **THEN** delete-all-data runs first, initialisation runs second, and the bulk load stores exactly the configured record count

#### Scenario: Fresh database
- **WHEN** a workload starts on a database that has never been used by the benchmark
- **THEN** delete-all-data succeeds without error, initialisation succeeds, and the bulk load stores exactly the configured record count
