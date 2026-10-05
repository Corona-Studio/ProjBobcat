# SharpJsonRepair AOT compatibility patch

Source: https://github.com/Corona-Studio/JsonRepairSharp
Revision: bc795160d99a2af316ae2e52d0f5c0d3dd7e4422 (NuGet 0.1.0).
License: MIT, retained in LICENSE.

The three string serialization calls use a generated JsonTypeInfo instead of
reflection-based JsonSerializer overloads. NativeAOT disables reflection-based
JSON serialization. The repair algorithm and public API are unchanged.
This source dependency can be replaced by an upstream AOT-compatible package
when that fix is published.
