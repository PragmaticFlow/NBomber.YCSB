# core-workload Specification

## Purpose

Defines the shared YCSB Core workload behaviour that applies to every database backend, so that workloads generate the same operations, keys and request sizes as the original YCSB.

## Requirements

### Requirement: Uniform scan length
Each Scan operation SHALL request a record count drawn uniformly at random from the inclusive range minscanlength..maxscanlength, independently for every scan.

#### Scenario: Scan length stays within bounds
- **WHEN** a workload performs scans with minscanlength 1 and maxscanlength 100
- **THEN** every scan requests between 1 and 100 records, and over many scans both 1 and 100 are requested

#### Scenario: Equal bounds give a fixed length
- **WHEN** minscanlength and maxscanlength are both 10
- **THEN** every scan requests exactly 10 records

### Requirement: Scan length defaults per workload
When no scan length is configured, minscanlength SHALL be 1, and maxscanlength SHALL be 100 for workload E and 1000 for every other workload, matching the original YCSB defaults and workload files.

#### Scenario: Workload E defaults
- **WHEN** workload E runs without minscanlength or maxscanlength
- **THEN** scans request between 1 and 100 records

#### Scenario: Default for other workloads
- **WHEN** any workload other than E is configured without minscanlength or maxscanlength
- **THEN** the effective scan length range is 1 to 1000

### Requirement: Explicit scan length overrides
An explicitly configured minscanlength or maxscanlength SHALL replace the corresponding default for any workload.

#### Scenario: Override maximum for workload E
- **WHEN** workload E runs with maxscanlength 500
- **THEN** scans request between 1 and 500 records

#### Scenario: Override minimum only
- **WHEN** workload E runs with minscanlength 20 and no maxscanlength
- **THEN** scans request between 20 and 100 records

### Requirement: Scan length validation
The configuration SHALL be rejected before the benchmark starts unless 1 <= minscanlength <= maxscanlength, where maxscanlength is the explicit value or the workload default.

#### Scenario: Minimum below one
- **WHEN** minscanlength is 0
- **THEN** validation fails with a message naming minscanlength and the benchmark does not run

#### Scenario: Minimum above maximum
- **WHEN** minscanlength is 50 and maxscanlength is 10
- **THEN** validation fails with a message naming both settings

#### Scenario: Minimum above workload default maximum
- **WHEN** workload E is configured with minscanlength 200 and no maxscanlength
- **THEN** validation fails because the effective maximum is 100

### Requirement: Operation latency covers only the database call
For every operation (read, read latest, update, insert, scan, read-modify-write), the key, field set, values and scan length SHALL be prepared before the operation's latency measurement starts, so the measured latency covers only the database call, as in original YCSB.

#### Scenario: Insert inputs prepared before timing
- **WHEN** an insert operation runs
- **THEN** its key and all field values are generated before the insert's measured step starts, and the step only sends them to the database

#### Scenario: Read inputs prepared before timing
- **WHEN** a read operation with readallfields false runs
- **THEN** its key and the single field name are chosen before the read's measured step starts

#### Scenario: Operation names and mix unchanged
- **WHEN** any workload A-F runs
- **THEN** the operations are reported under the same names and in the same proportions as before

### Requirement: Generated field values
Each generated field value SHALL be exactly fieldlength characters, each a lowercase letter a to z.

#### Scenario: Default field length
- **WHEN** a record is generated with fieldlength 100
- **THEN** every field value is 100 characters long and contains only the letters a to z

#### Scenario: Field length of one
- **WHEN** a record is generated with fieldlength 1
- **THEN** every field value is a single lowercase letter

### Requirement: Field set of generated records
An inserted record and an update with writeallfields true SHALL contain exactly the fields field1 to fieldN, where N is fieldcount, each with a generated value.

#### Scenario: Full record
- **WHEN** a record is generated with fieldcount 10
- **THEN** it has exactly the fields field1 to field10

### Requirement: Single field selection
When readallfields is false a read SHALL request one field, and when writeallfields is false an update SHALL write one field, each chosen uniformly at random from field1 to fieldN.

#### Scenario: Selection stays within the field set
- **WHEN** many single-field reads and updates are generated with fieldcount 10
- **THEN** every chosen field is one of field1 to field10, and both field1 and field10 are chosen

#### Scenario: Single field record
- **WHEN** fieldcount is 1 and a single field is chosen
- **THEN** the chosen field is field1

### Requirement: Concurrent data generation
Generation of keys, field names and values SHALL be safe and independent across concurrent scenario copies, without a lock shared by all copies.

#### Scenario: Parallel generation
- **WHEN** many records and single-field choices are generated in parallel from several threads
- **THEN** no exception occurs and every result satisfies the field value and field set requirements

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

### Requirement: Scrambled zipfian key choice
Read, update, scan and read-modify-write operations SHALL choose keys with YCSB's scrambled zipfian distribution: a Zipfian rank with constant 0.99 over 10^10 items, scrambled with YCSB's 64-bit FNV hash and mapped onto keys 1 to itemcount as 1 + fnvhash64(rank) mod itemcount.

#### Scenario: Rank to key mapping
- **WHEN** recordcount is 1000 with no inserts and the Zipfian rank is 0, 1 or 2
- **THEN** the key is 145, 611 or 214 respectively (1 + fnvhash64(rank) mod 1001)

#### Scenario: Skew matches YCSB
- **WHEN** 200000 keys are chosen with recordcount 1000 and no inserts
- **THEN** the most chosen key gets between 3% and 5% of the choices and the ten most chosen keys together get between 10% and 17%

#### Scenario: Keys stay in range
- **WHEN** keys are chosen with recordcount 1000 and no inserts
- **THEN** every chosen key is between 1 and 1000

### Requirement: Zipfian item count
The zipfian item count SHALL be recordcount plus the expected new keys plus one, matching YCSB's inclusive key range, where the expected new keys are operationcount times the workload's insert proportion times 2, rounded down.

#### Scenario: Workloads without inserts
- **WHEN** the workload is A, B, C or F with recordcount 1000 and operationcount 1000
- **THEN** the item count is 1001

#### Scenario: Workload E
- **WHEN** the workload is E with recordcount 1000 and operationcount 1000
- **THEN** the item count is 1101

### Requirement: Zipfian keys above the acknowledged limit are redrawn
A zipfian key above the acknowledged key limit, or below 1, SHALL be redrawn until it is between 1 and the limit, so the distribution over existing keys keeps its shape. Creating the key generator with recordcount 0 for a workload that chooses zipfian or read-latest keys (A, B, C, D, E, F) SHALL fail with an error naming recordcount; no key SHALL be returned for an empty key range.

#### Scenario: Redraw is deterministic
- **WHEN** recordcount is 3 (item count 4) and rank 0 maps to key 4
- **THEN** key 4 is redrawn and the key for the next rank is returned

#### Scenario: Unacknowledged keys never chosen
- **WHEN** workload E has recordcount 1000, item count 1101 and acknowledged key limit 1000
- **THEN** every chosen key is between 1 and 1000

#### Scenario: Acknowledged new keys become reachable
- **WHEN** the acknowledged key limit grows to 1100
- **THEN** keys between 1001 and 1100 can be chosen and no key above 1100 is chosen

#### Scenario: Empty dataset rejected
- **WHEN** workload A, B, C, D, E or F is set up with recordcount 0
- **THEN** creation fails with an error naming recordcount before any database operation

### Requirement: YCSB FNV-64 hash
The scrambling hash SHALL produce the same values as YCSB's `Utils.fnvhash64` for the same input, observable through the chosen keys.

#### Scenario: Mapping at a second item count
- **WHEN** recordcount is 1000000 with no inserts (item count 1000001) and the Zipfian rank is 0, 1 or 2
- **THEN** the key is 801321, 216075 or 971812 respectively

### Requirement: Item count arithmetic is safe for large values
The item count SHALL be computed without integer overflow or loss of precision for any recordcount and operationcount that the options accept, and creation SHALL fail with an error naming recordcount if the item count cannot be represented.

#### Scenario: Large recordcount and operationcount
- **WHEN** the workload is E with recordcount 2147483647 and operationcount 2147483647
- **THEN** the item count is 2362232012

#### Scenario: Unrepresentable recordcount
- **WHEN** recordcount is the largest unsigned 64-bit value
- **THEN** creation fails with an error naming recordcount

### Requirement: Small datasets
Key choice SHALL terminate and SHALL never choose an unfinished insert for datasets of one or two records.

#### Scenario: One record
- **WHEN** recordcount is 1
- **THEN** every chosen key is 1, including when an insert has reserved key 2 but not acknowledged it

#### Scenario: Two records
- **WHEN** recordcount is 2 and an insert has reserved key 3 but not acknowledged it
- **THEN** every chosen key is 1 or 2 and both are chosen

### Requirement: Read latest key distribution
Read latest SHALL choose its key like YCSB's `SkewedLatestGenerator`: with n the acknowledged key limit, a rank r SHALL be drawn from YCSB's Zipfian draw (Gray et al.) with constant 0.99 over n items using ζ(n, 0.99), and the key SHALL be n − r, between 1 and n. The newest acknowledged key SHALL be the most likely key.

#### Scenario: Newest key is the hottest
- **WHEN** workload D runs with recordcount 1000 and no acknowledged inserts
- **THEN** key 1000 is chosen in about 12.9% of read-latest operations (1/ζ(1000, 0.99)), key 999 in about 6.5%, and no key outside 1..1000 is chosen

#### Scenario: Newly acknowledged key becomes the hottest
- **WHEN** an insert of key 1001 is acknowledged
- **THEN** key 1001 becomes the most likely read-latest key and no key above 1001 is chosen

#### Scenario: Single record
- **WHEN** recordcount is 1 and no insert is acknowledged
- **THEN** read latest always chooses key 1

#### Scenario: Missing key
- **WHEN** a chosen key is not found by the database
- **THEN** the operation returns `Response.Fail(statusCode: "no data")`, as for any read

### Requirement: Read latest key choice cost
Choosing a read-latest key SHALL take constant time, independent of the number of keys, and SHALL NOT take a lock. ζ(recordcount, 0.99) SHALL be computed when the key generator is created, before any measured operation. When the acknowledged key limit grows, ζ SHALL be extended, not recomputed, at a cost proportional to the number of newly acknowledged keys. Each draw SHALL use a ζ that matches the n used for the draw.

#### Scenario: Large dataset
- **WHEN** workload D runs with recordcount 1,000,000
- **THEN** each read-latest key choice costs microseconds or less, not time proportional to 1,000,000

#### Scenario: Concurrent growth
- **WHEN** several scenario copies choose read-latest keys while inserts are acknowledged
- **THEN** every draw uses a consistent (n, ζ(n)) pair and returns a key between 1 and that n
