## MODIFIED Requirements

### Requirement: Reset benchmark data
The PostgreSQL client's delete-all-data operation SHALL drop the benchmark table, removing every record, so that after initialisation a subsequent bulk load produces exactly the configured record count with freshly generated field values.

#### Scenario: Leftover records from a previous run are removed
- **WHEN** the benchmark table already contains records from an earlier run, including keys that the next bulk load will insert again
- **THEN** after delete-all-data and initialisation succeed the table contains no records, and the following bulk load stores, for every key, exactly the newly generated field set and values (no fields or values from the earlier run remain)

#### Scenario: Reset on an empty table
- **WHEN** delete-all-data runs on an existing, empty benchmark table
- **THEN** the operation succeeds and, after initialisation, the table is empty

#### Scenario: Reset when the table does not exist
- **WHEN** delete-all-data runs and the benchmark table does not exist
- **THEN** the operation succeeds

### Requirement: Reset is limited to the benchmark table
Delete-all-data SHALL drop only the benchmark table. It MUST NOT remove or alter other tables, schemas or databases. Initialisation SHALL create the benchmark table with the YCSB schema (primary key and JSONB value column, no other index) when it does not exist.

#### Scenario: Other data is untouched
- **WHEN** the database contains other tables alongside the benchmark table
- **THEN** delete-all-data leaves those tables and their rows unchanged

#### Scenario: Table remains usable after reset
- **WHEN** delete-all-data and then initialisation succeed
- **THEN** the benchmark table exists with the primary-key and JSONB columns and no other index, and inserts, reads and updates succeed

#### Scenario: Leftover index is removed
- **WHEN** the benchmark table has an extra index from an earlier version and delete-all-data then initialisation run
- **THEN** the benchmark table has only its primary key
