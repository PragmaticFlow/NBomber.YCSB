## ADDED Requirements

### Requirement: Separate reset and initialisation
Delete-all-data SHALL drop the benchmark table if it exists and SHALL succeed when the table or the keyspace does not exist; it MUST NOT drop other tables or the keyspace. Initialisation SHALL create the keyspace if missing and the benchmark table with one text column per configured field, and SHALL prepare the statements, so the client can insert, read, update and scan. Initialisation SHALL NOT drop data; when the table already exists it is kept as is.

#### Scenario: Reset then initialise after a previous run
- **WHEN** the benchmark table holds records from an earlier run and delete-all-data then initialisation run
- **THEN** the table is empty, has the columns `field1..fieldN` for the current field count, and the following bulk load and operations succeed

#### Scenario: Field count changed between runs
- **WHEN** an earlier run used 10 fields and the next run uses 20
- **THEN** after delete-all-data and initialisation the table has `field1..field20`

#### Scenario: Fresh cluster
- **WHEN** neither the keyspace nor the table exists and delete-all-data runs
- **THEN** the operation succeeds and creates nothing (no keyspace, no table)

#### Scenario: Other tables are untouched
- **WHEN** the keyspace contains another table
- **THEN** delete-all-data leaves it unchanged
