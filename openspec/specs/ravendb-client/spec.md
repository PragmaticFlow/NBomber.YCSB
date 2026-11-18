# ravendb-client Specification

## Purpose
Defines the observable reset and initialisation behaviour of the RavenDB backend of the YCSB benchmark.

## Requirements

### Requirement: Reset and initialisation
Delete-all-data SHALL hard-delete only the configured benchmark database and SHALL succeed when it does not exist. Initialisation SHALL create the benchmark database and warm up the connection.

#### Scenario: Fresh server
- **WHEN** the benchmark database does not exist and delete-all-data runs, followed by initialisation
- **THEN** delete-all-data succeeds, initialisation creates the database, and the bulk load succeeds

#### Scenario: Previous run's data removed
- **WHEN** the benchmark database holds documents from an earlier run and delete-all-data then initialisation run
- **THEN** the database exists and holds no documents
