# AgentUp.Sdk.Common

Shared contracts for the first-party Agent-Up capabilities: identity, the closed capability kinds, the software a capability needs on the Server host, the settings a user configures for it, and Nix package declarations.

Capabilities are compiled into the Server. These contracts are not a plugin or package interface; they are how the Server reads every built-in capability the same way.
