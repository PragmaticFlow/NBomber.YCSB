## ADDED Requirements

### Requirement: Reset is limited to the benchmark database
Delete-all-data SHALL drop only the benchmark database (`ycsb`), removing all its collections, and MUST NOT drop or alter any other database on the server.

#### Scenario: Other databases are untouched
- **WHEN** the server holds another database alongside `ycsb`
- **THEN** after delete-all-data that database and its collections are unchanged, and `ycsb` holds no documents

#### Scenario: Load after reset
- **WHEN** delete-all-data succeeds and the bulk load runs
- **THEN** the benchmark collection contains exactly the loaded records
