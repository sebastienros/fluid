## Testing

When running test use a single TFM (net10.0) to improve dev loop time.

### Golden Tests

The `GoldenLiquidTests.cs` file contains tests from the Golden Liquid tests suite (https://github.com/jq-rp/golden-liquid). The test definitions come from `https://raw.githubusercontent.com/jg-rp/golden-liquid/main/golden_liquid.json` and are not statically mentioned in this class, only in the JSON document.

When running these Golden Liquid tests filter you can filter a single test by using the filter `DisplayName~[GROUP_NAME]/[TEST_NAME]` with the group and test names separated by / like in this command line:

```shell
dotnet test -f net10.0 --filter "DisplayName~liquid.golden.date_filter/missing argument"
```

Golden Liquid tests might invalidate other existing unit tests so updating the unit tests are fine if they contradict a Golden Liquid test. Golden Tests always prevail.
