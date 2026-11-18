## ADDED Requirements

### Requirement: Schema matches YCSB
The benchmark table SHALL have only the primary-key column and the JSONB value column, with no secondary index on the value. Initialisation SHALL NOT create an index on the value column. An index `idx_ycsb_table_jsonb` left by a previous version is removed by the reset, which drops the table; initialisation recreates it without the index (change scope-delete-all-data).

#### Scenario: Fresh database
- **WHEN** initialisation runs and the benchmark table does not exist
- **THEN** the table is created with the primary key only and no index on the JSONB column

### Requirement: Prepared statements
Repeated statements SHALL be prepared on the server after repeated use (automatic preparation after 5 uses), and projected reads SHALL use the same SQL text whichever field is requested.

#### Scenario: Projected reads of different fields
- **WHEN** reads request `field1` and later `field7`
- **THEN** both use the same SQL text with the field name as a parameter

### Requirement: Update reports a missing record
Update and the write phase of read-modify-write SHALL return `Response.Fail(statusCode: "no data")` when no row has the given key, and SHALL keep merging the given fields into the stored value otherwise.

#### Scenario: Update a missing key
- **WHEN** an update targets a key that does not exist
- **THEN** the operation returns `Response.Fail(statusCode: "no data")` and no row is created

#### Scenario: Update keeps other fields
- **WHEN** `field2` of an existing record is updated
- **THEN** `field2` has the new value and every other field keeps its value
