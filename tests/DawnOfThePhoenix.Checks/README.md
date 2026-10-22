# DawnOfThePhoenix.Tests

This xUnit/Shouldly project verifies... It references...

```sh
dotnet test DawnOfThePhoenix.Tests/DawnOfThePhoenix.Tests.csproj
```

Use the SDK described in the [root README](../README.md).

## Test conventions

Use short, outcome-first names for new and changed tests; some older names vary:

```text
{DomainType}Should.{ExpectedBehavior}_On_{Member}_{Scenario}
EmploymentPeriodShould.ReturnNewValueAndPreserveOriginal_On_ReplaceExited_WhenDateIsValid
```

- Name classes `{DomainType}Should`; use PascalCase within phrases and underscores
  between them. Match the production member name when practical.
- Put observable behavior first, then a precise `When...` scenario if it adds
  value. Use a clearer connector such as `After` when needed.
- Test concrete types and one behavior at a time; avoid implementation details.
- Cover normalization, boundaries, equality, present/absent optional values, and
  invalid inputs. Assert unchanged aggregate state after failure and unchanged
  original values after immutable transformations.
- Cover collection duplicates, primary cardinality, and membership snapshots.
  Use theories for boundaries and fixed caller-supplied dates where supported.
- Add provider-backed round-trip and corrupt-row tests when aggregate persistence
  is introduced. Converter tests alone do not prove aggregate persistence works.

Use code, existing tests, and coverage reports to find gaps. Keep test guidance
about behavior and conventions rather than maintaining duplicate coverage lists
or historical test counts.
