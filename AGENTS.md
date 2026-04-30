
## Reference Implementation

The reference implementation of the Liquid template language in Ruby can be found at https://github.com/Shopify/liquid

Refer to this implementation when the specification is unclear.

## Testing

When running test use a single TFM (net10.0) to improve dev loop time.

### Golden Tests

The `GoldenLiquidTests.cs` file contains tests from the Golden Liquid tests suite (https://github.com/jq-rp/golden-liquid). The test definitions come from `https://raw.githubusercontent.com/jg-rp/golden-liquid/main/golden_liquid.json` and are not statically mentioned in this class, only in the JSON document.

Golden Liquid tests might invalidate other existing unit tests so updating the unit tests are fine if they contradict a Golden Liquid test. Golden Tests always prevail.

#### Running a Single Golden Test

With xUnit v3 and MTP v2, use the test executable directly with the `-id` option:

**Step 1: Find the test ID**
```shell
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -list full 2>&1 | grep -B2 -A5 "YOUR_TEST_NAME"
```

**Step 2: Run the test by ID**
```shell
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -id "TEST_ID_HERE"
```

**Example:**
```shell
# Find the test ID for identifiers_ascii_lowercase
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -list full 2>&1 | grep -B2 -A5 "identifiers_ascii_lowercase"

# Run it (use the ID from the output above)
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -id "71958641a76ed3a8219c73a9e5f956b4ecf2cb1b07ca728d3d8c8365646e7895"
```

**Note:** The `-preEnumerateTheories` flag is required to enumerate the parameterized tests properly.

#### Running All Golden Tests

To run all Golden Liquid tests:
```shell
./Fluid.Tests/bin/Debug/net10.0/Fluid.Tests -preEnumerateTheories -method "Fluid.Tests.GoldenLiquidTests.GoldenTestShouldPass"
```

#### Listing all tests

To list all unit tests, at the github root, run:
```shell
dotnet test --list-tests
```