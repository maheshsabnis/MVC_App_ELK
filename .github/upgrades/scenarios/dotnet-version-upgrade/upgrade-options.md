# Upgrade Options — MVC_App

Assessment: One SDK-style .NET 8 project targeting .NET 10; four recommended package upgrades and one behavioral API change.

## Strategy

### Upgrade Strategy
A single project has no cross-project dependencies to sequence.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade the project and its packages together, then validate the application. |
| Top-Down | Upgrade entry-point applications first and temporarily multi-target shared libraries where needed. |
