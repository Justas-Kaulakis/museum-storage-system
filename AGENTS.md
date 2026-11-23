# AGENTS.md

## Project overview

This repository contains a university cyber-physical engineering project: a museum storage and archiving system.

The system has two major parts:

1. A physical embedded subsystem that monitors storage conditions and protects exhibits.
2. A server-side information system that stores telemetry, manages exhibits and storage operations, exposes a REST API, and later provides a Web UI.

The physical protection subsystem must remain operational even when the server, database, network, or Wi-Fi connection is unavailable.

## Core architectural rule

The server must never be required for real-time environmental protection.

The embedded controller must locally:
- read temperature;
- read humidity;
- read light level;
- control the fan;
- control the buzzer/alarm;
- show local status;
- continue using the last valid environmental configuration when the server is unavailable.

The server may provide configuration values, telemetry storage, history, management, Web UI, alerts, and QR/storage functionality, but it must not be in the real-time control loop for the fan or buzzer.

The intended control flow is:

Sensors -> MCU -> fan / buzzer / local display

The server-side flow is:

MCU -> HTTP API -> database -> Web UI / history / alerts

## Offline behavior

Use offline model A for storage operations.

If the server is unavailable:
- environmental monitoring and protection continue locally;
- fan and buzzer logic continue locally;
- the last valid configuration remains in use;
- QR-based exhibit lookup and storage operations are temporarily unavailable;
- do not implement an offline exhibit database, offline storage-operation queue, conflict resolution, or synchronization logic on the MCU.

## Final physical hardware

The final university hardware is expected to use:
- Arduino Nano 33 IoT;
- DHT11 temperature and humidity sensor;
- photoresistor/LDR with a voltage divider;
- relay module;
- 12 V fan;
- buzzer;
- ArduCAM Mini OV5642 camera module;
- QR labels attached to exhibits;
- local display if useful;
- Wi-Fi network;
- Ubuntu home-lab laptop as the server.

The final Nano 33 IoT uses 3.3 V logic. Real hardware integration must account for voltage levels and pull-ups before connecting 5 V modules.

## Current simulation

The physical prototype is currently simulated in Wokwi.

Simulation hardware:
- ESP32 DevKit v1 as a stand-in for Nano 33 IoT;
- DHT22 as a stand-in for DHT11;
- Wokwi photoresistor sensor;
- SSD1306 128x64 OLED;
- relay module;
- LED as the fan/load indicator;
- buzzer.

The Wokwi simulation is a development aid only. Keep application/control logic portable so it can later be moved to Arduino Nano 33 IoT.

Current physical control logic:
- fan turns on when temperature >= 26.0 C OR humidity >= 70.0%;
- fan turns off only when temperature <= 25.0 C AND humidity <= 65.0%;
- this hysteresis is intentional;
- alarm turns on when temperature >= 30.0 C OR humidity >= 80.0%;
- the simulated relay is active-low;
- fan control and alarm decisions are made locally on the MCU.

These values are defaults only. They must become configurable through the server.

## QR and storage architecture

Exhibits are identified by QR codes.

The QR code should contain a stable exhibit identifier, for example:

EXHIBIT-0001

Do not place the full exhibit record inside the QR code.

The intended future flow is:

QR code -> ArduCAM -> MCU -> image upload to server -> QR decoding -> exhibit lookup -> storage operation

QR decoding should be performed on the server, not on the Nano 33 IoT.

Planned storage operations:
- view exhibit information;
- place/check an exhibit into storage;
- remove/check an exhibit out of storage;
- move an exhibit to another storage location;
- record the operation timestamp and history.

If the server is unavailable, these storage operations are unavailable.

## Functional requirements

The complete system should eventually support:

### Environmental monitoring
- read temperature;
- read humidity;
- read light level;
- periodically report measurements to the server;
- store measurement history;
- display current values in the Web UI;
- display current fan state;
- display current alarm state.

### Environmental control
- configurable fan-on temperature;
- configurable fan-off temperature;
- configurable fan-on humidity;
- configurable fan-off humidity;
- configurable critical temperature;
- configurable critical humidity;
- local fan control using temperature and humidity;
- local alarm/buzzer control;
- hysteresis to prevent relay chattering;
- continued local operation if the server is unavailable.

### Configuration
- environmental thresholds are managed on the server;
- the MCU periodically obtains server configuration;
- the MCU must use a last-known-valid configuration when disconnected;
- configuration must be validated before use;
- safe defaults must exist.

### Exhibit management
- add exhibits;
- edit exhibits;
- remove exhibits where appropriate;
- search exhibits;
- identify exhibits using QR codes;
- store exhibit name;
- stable identifier;
- description;
- category;
- current state;
- storage location.

### Storage management
- maintain storage locations;
- check exhibits into storage;
- check exhibits out of storage;
- move exhibits between storage locations;
- record all operations in history;
- record timestamps.

### Alerts
- record environmental alerts;
- expose alerts through the Web UI;
- optional email notifications may be added later.

### Web UI
The future Web UI should provide:
- dashboard/current environment;
- exhibit list;
- exhibit details;
- storage locations;
- exhibit operation history;
- environmental history;
- alerts;
- environmental configuration.

## Non-functional requirements currently agreed

- The physical environmental protection system must operate autonomously.
- Server failure must not disable fan or buzzer control.
- Network failure must not disable fan or buzzer control.
- The backend must run on both Windows and Linux/Ubuntu.
- Persist server-side data in a database.
- Use UTC timestamps for persisted events and telemetry.
- Validate API input.
- Keep the implementation understandable for a university engineering project.
- Avoid unnecessary distributed-system complexity.
- Avoid premature abstractions and overengineering.
- Prefer deterministic, testable control logic.
- Preserve backwards compatibility with already working endpoints unless a task explicitly requires a breaking change.
- Add automated tests for important server-side behavior.
- Security should be considered when the system is exposed beyond the local network.
- Tailscale will later be used for remote access to the Ubuntu home-lab server, but current development is local.

This list is not assumed to be the final formal non-functional-requirements section of the report. If a task depends on a requirement not listed here, do not invent it silently.

## Backend technology

Current backend stack:
- .NET 10;
- ASP.NET Core Web API;
- controllers;
- Entity Framework Core;
- SQLite;
- xUnit tests.

Current solution structure:

backend/
- MuseumStorage.Api/
- MuseumStorage.Domain/
- MuseumStorage.Infrastructure/
- MuseumStorage.Tests/
- MuseumStorage.slnx

Do not replace the current architecture with minimal APIs or a different framework without an explicit reason.

## Current backend state

The backend already has working sensor-reading persistence.

Current entity:
- SensorReading

Current stored fields:
- Id;
- DeviceId;
- Temperature;
- Humidity;
- LightLevel;
- FanEnabled;
- AlarmEnabled;
- CreatedAt.

Current endpoint behavior:
- POST /api/sensor-readings stores a reading;
- GET /api/sensor-readings returns recent readings newest-first.

This behavior has already been manually tested successfully against SQLite.

Current development URLs:
- HTTPS: https://localhost:7204
- HTTP: http://localhost:5054

Do not hard-code these ports into application/domain code.

## Deployment

Development is currently performed on Windows.

The production/home-lab target is Ubuntu Linux.

The same .NET application should run on both platforms without OS-specific application logic.

Tailscale will be configured later. Do not make Tailscale a dependency of the application.

Containerization may be introduced later but is not required for the current development stage.

## Code style

Follow the existing language conventions and these project-specific preferences.

For C/C++:
- keep function calls on one line;
- do not add decorative separator comments such as `// ---`;
- if an `if` body contains one statement, omit braces;
- keep empty lines to a minimum;
- use empty lines only when they improve readability;
- keep control logic straightforward and readable.

For C#:
- follow normal modern C#/.NET formatting;
- do not create unnecessary helper layers or interfaces;
- prefer clear names over excessive comments;
- keep simple one-line calls on one line where reasonable;
- keep empty lines purposeful;
- avoid decorative comment separators.

For shell/PowerShell commands:
- every command shown to the user must be on one physical line;
- never split a command with backticks, backslashes, carets, or line continuations.

## Development rules for agents

Before making changes:
1. Inspect the current repository and existing implementation.
2. Do not assume files are identical to examples from chat history.
3. Preserve working code unless the requested change requires modification.
4. State important architectural conflicts before implementing around them.

When implementing:
- make focused changes;
- add/update tests;
- run build and tests;
- fix warnings/errors caused by your changes;
- do not rewrite unrelated files;
- do not add packages unless they are needed;
- do not commit secrets;
- do not hard-code machine-specific paths;
- use UTC for persisted server timestamps.

When reporting completion:
- summarize changed files;
- summarize implemented behavior;
- list build/test commands executed;
- report test results;
- mention any remaining limitations or decisions that require user input.

## Immediate development direction

The next backend feature is server-managed environmental configuration.

The server should persist configurable thresholds such as:
- fan-on temperature;
- fan-off temperature;
- fan-on humidity;
- fan-off humidity;
- critical/alarm temperature;
- critical/alarm humidity.

The server stores and exposes these values.

The MCU remains responsible for applying them locally.

The server must not directly command the fan based on incoming readings.
