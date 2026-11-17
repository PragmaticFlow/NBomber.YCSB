# Spec Delta

## ADDED Requirements

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
A zipfian key above the acknowledged key limit, or below 1, SHALL be redrawn until it is between 1 and the limit, so the distribution over existing keys keeps its shape. Creating the key generator with recordcount 0 for a workload that chooses zipfian keys SHALL fail with an error naming recordcount; no key SHALL be returned for an empty key range.

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
- **WHEN** workload A, B, C, E or F is set up with recordcount 0
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
