# Client diagnostics

The desktop client owns one `ILogger` boundary. It writes JSON records to
standard error through the .NET console provider. Event templates are
source-generated. The queue holds up to 256 records and may drop writes
under backpressure so logging cannot stall a frame waiting on the sink.

Only event-specific numeric fields are emitted. The client does not pass
exception objects, stack traces, exception messages, account information,
tickets, paths, package names, endpoint details or raw packet bytes to
this logger. A fatal managed client failure produces event 2101 and
returns exit code 1. Invalid startup arguments produce event 2100 with
usage and return exit code 2. Platform and native libraries own their
own output.

## Events

| ID | Outcome | Context |
| --- | --- | --- |
| 2100 | Startup arguments rejected | Argument code |
| 2101 | Client stopped after a managed failure | Failure code |
| 2102 | Cleanup failed while handling a client failure | Failure code |
| 2103 | Splash unavailable | |
| 2104 | Package declaration skipped | One-based declaration index and outcome |
| 2200 | Revoked login session cleanup failed | Failure code |
| 2201 | Role creation content unavailable | Failure code |
| 2202–2204 | Account history unavailable, invalid, or could not be saved | Failure code |
| 2205 | Audio unavailable | Failure code |
| 2206 | Clipboard unavailable | |
| 2207–2208 | Realm authority unconfigured, or catalog unavailable or rejected | |
| 2300 | World connection closed | Failure code |
| 2301 | Required world entry content unavailable | Failure code |
| 2302 | Optional role mesh unavailable | |
| 2303–2304 | Role or presentation content unavailable | Failure code |
| 2305 | Optional model unavailable | Retail model ID and equipment slot |
| 2306 | Unsupported world packet or action | Packet type and action code, with 0 for no action |
| 2307 | Unsupported attribute group | IDs 0–54 and 128, or 4294967295 for all other IDs |
| 2308 | World packet diagnostic limit reached | Maximum distinct packet/action pairs |

A world reports each unsupported packet/action pair once, up to 64 pairs.
It emits one limit record at that point. Unsupported attribute warnings
are deduplicated separately. These limits affect diagnostics alone.

## Codes

Failure codes group errors by recovery context. Code 0 covers unclassified
exceptions.

| Code | Failure |
| --- | --- |
| 1 | File or directory missing |
| 2 | Invalid data |
| 3 | Access denied |
| 4 | I/O |
| 5 | Socket |
| 6 | Timeout |
| 7 | Native library or entry point unavailable |
| 8 | Unsupported operation |
| 9 | Canceled |
| 10 | Invalid state |
| 11 | Invalid argument |

Argument code 1 means unknown argument. Codes 2–4 identify duplicate,
missing and invalid content-root values, respectively. Codes 5–7 apply
the same ordering to presentation, codes 8–10 to window mode, and codes
11–13 to window size. Package outcome 2 means a duplicate prefix and 3
means the archive was unavailable. Equipment slots 1, 4 and 5 identify
headwear, right weapon and left weapon.

The launcher has its own diagnostics and lifecycle.
