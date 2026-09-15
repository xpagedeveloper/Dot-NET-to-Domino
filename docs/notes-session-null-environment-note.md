# Null environment values

XPScript's current `SetEnvironmentVar` implementation does not accept null as a valid value; it falls through to runtime error 13. The .NET wrapper now follows that behavior instead of converting null to an empty string.
