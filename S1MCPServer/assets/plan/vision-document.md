# S1MCPServer Vision Document

## Executive Summary

S1MCPServer is a MelonLoader mod for Schedule I that enables agentic LLM access to game objects and scripting through a Model Context Protocol (MCP) server. The goal is to enable agentic debugging of mods and the game itself, providing real-time inspection and manipulation capabilities for development and troubleshooting.

## Project Goals

1. **Enable Agentic Debugging**: Allow LLM agents to inspect game state, diagnose issues, and suggest fixes for mods and game behavior
2. **Real-time Game Access**: Provide live access to Schedule I game objects without requiring game restarts
3. **Scripting Capabilities**: Enable dynamic execution of game operations through a safe, controlled interface
4. **Developer Tooling**: Create a powerful debugging and development tool for the Schedule I modding community

## Architecture Overview

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                    LLM Agent (Claude/GPT)                     │
└────────────────────────────┬──────────────────────────────────┘
                             │ MCP Protocol (JSON-RPC)
                             │
┌────────────────────────────▼──────────────────────────────────┐
│              Standalone MCP Server                            │
│  - Handles MCP protocol (JSON-RPC over stdio)                  │
│  - Translates MCP tools → Game API calls                     │
│  - Manages LLM interactions                                  │
│  - Language: Python/Node.js/C# (TBD)                         │
└────────────────────────────┬──────────────────────────────────┘
                             │ Named Pipe Communication
                             │ (Windows IPC)
┌────────────────────────────▼──────────────────────────────────┐
│              MelonLoader Mod (S1MCPServer)                   │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Named Pipe Server (Background Thread)                 │  │
│  │  - Listens for commands from MCP server                │  │
│  │  - Receives JSON-RPC-like requests                     │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Enqueue Commands                            │
│  ┌──────────────▼───────────────────────────────────────────┐  │
│  │  Command Queue (Thread-Safe)                            │  │
│  │  - Queues game operations                               │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Process on Main Thread                      │
│  ┌──────────────▼───────────────────────────────────────────┐  │
│  │  Game API Layer (Main Thread)                           │  │
│  │  - Accesses Unity game objects                          │  │
│  │  - Uses custom game API (based on S1API patterns)        │  │
│  │  - Executes game operations safely                      │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Results                                     │
│  ┌──────────────▼───────────────────────────────────────────┐  │
│  │  Response Queue (Thread-Safe)                          │  │
│  │  - Queues operation results                            │  │
│  └──────────────┬───────────────────────────────────────────┘  │
│                 │ Send via Named Pipe                          │
│                 └──────────────────────────────────────────────┘
└────────────────────────────┬──────────────────────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────────┐
│              Schedule I Game (Unity)                          │
│  - Game objects (NPCs, Items, Vehicles, Buildings)           │
│  - Player state and inventory                                 │
│  - Network state (multiplayer)                                │
│  - Game systems (native Unity/Schedule One classes)         │
└───────────────────────────────────────────────────────────────┘
```

### Communication Protocol

**Transport**: Windows Named Pipes (`\\.\pipe\S1MCPServer`)
- Efficient local IPC
- Low overhead
- Built-in Windows support
- Thread-safe for background/main thread communication

**Message Format**: JSON-RPC 2.0-like protocol
```json
// Request
{
  "id": 1,
  "method": "get_npc",
  "params": {
    "npc_id": "kyle_cooley"
  }
}

// Response
{
  "id": 1,
  "result": {
    "npc_id": "kyle_cooley",
    "name": "Kyle Cooley",
    "position": {"x": 10.5, "y": 1.0, "z": 20.3},
    "is_conscious": true,
    "health": 100.0
  },
  "error": null
}
```

## Game Objects and Systems to Expose

### 1. NPCs (Non-Player Characters)
**Read Operations:**
- List all NPCs
- Get NPC by ID
- Get NPC state (position, health, consciousness, panic status)
- Get NPC relationships
- Get NPC schedule
- Get NPC inventory
- Get NPC appearance/customization

**Write Operations:**
- Modify NPC health
- Teleport NPC to position
- Modify NPC relationships
- Trigger NPC actions (move, interact)
- Modify NPC schedule
- Modify NPC appearance

**Custom Implementation (S1API Reference):**
- Create custom NPC management utilities based on S1API patterns
- Access runtime NPC properties via native game classes
- Implement relationship system access using reflection
- Access scheduling system via game's native APIs
- **S1API Reference**: Study S1API's NPC management patterns for cross-runtime compatibility

### 2. Items
**Read Operations:**
- List all item definitions
- Get item by ID
- Get item properties (name, description, category, pricing)
- List items in player inventory
- List items in world

**Write Operations:**
- Spawn item in world
- Add item to player inventory
- Remove item from inventory
- Modify item properties (runtime)

**Custom Implementation (S1API Reference):**
- Create custom item creation utilities based on S1API ItemCreator patterns
- Access Registry.Instance.ItemRegistry directly (native game class)
- Implement item management utilities with cross-runtime support
- **S1API Reference**: Study S1API's ItemCreator and Registry access patterns

### 3. Player
**Read Operations:**
- Get player position
- Get player health/state
- Get player inventory
- Get player money
- Get player relationships
- Get player network status (host/client/singleplayer)

**Write Operations:**
- Teleport player
- Modify player health
- Add/remove items from inventory
- Modify player money
- Modify player relationships

### 4. Buildings
**Read Operations:**
- List all buildings
- Get building by name/ID
- Get building position
- Get building properties
- List NPCs in building

**Custom Implementation (S1API Reference):**
- Implement building access utilities based on S1API Building patterns
- Access buildings via native game classes with cross-runtime support
- **S1API Reference**: Study S1API's Building.GetAll(), Building.Get<T>(), and Building.GetByName() patterns

### 5. Vehicles
**Read Operations:**
- List all vehicles
- Get vehicle by ID
- Get vehicle position
- Get vehicle state
- Get occupants

**Write Operations:**
- Spawn vehicle
- Teleport vehicle
- Modify vehicle state

### 6. Game State
**Read Operations:**
- Get current scene
- Get game time
- Get network status
- Get loaded mods
- Get game version

**Write Operations:**
- Change scene (if safe)
- Modify game time (if possible)

### 7. Debugging & Inspection
**Operations:**
- Get object hierarchy
- Inspect component properties
- Get Unity object references
- Read/write component fields (with safety checks)
- Execute C# code snippets (sandboxed, optional)
- Access Unity GameObjects via UnityExplorer (optional integration)

**UnityExplorer Integration:**
- UnityExplorer is a universal Unity debugging tool that works well with Schedule I
- **Repository**: https://github.com/yukieiji/UnityExplorer/ (forked from sinai-dev/UnityExplorer)
- Built on UniverseLib framework (git submodule dependency)
- Primarily a UI tool for in-game inspection and debugging
- Compatible with both IL2CPP and Mono backends
- Provides C# console for runtime script execution
- Supports Unity versions 5.2 to 2021+ (IL2CPP and Mono)

**UnityExplorer Architecture:**
- Uses UniverseLib for reflection and object inspection
- Provides `InspectorManager` class with public API for programmatic access
- Provides ReflectionManager internally
- Built on UniverseLib framework (https://github.com/yukieiji/UniverseLib)
- Primarily UI-driven tool, but has programmatic API

**Integration Research Findings:**
1. **Public API Available**: UnityExplorer exposes `InspectorManager` class for programmatic object inspection
   - `UnityExplorer.InspectorManager.Inspect(object)` - Inspect an object
   - `UnityExplorer.InspectorManager.Inspect(Type)` - Inspect a Type
2. **Reflection-Based**: UnityExplorer uses standard C# reflection (System.Reflection) and Il2CppInterop
3. **UniverseLib Foundation**: Built on UniverseLib which provides reflection utilities
   - UniverseLib is a git submodule in UnityExplorer repository
   - Can be used independently as a dependency
4. **Runtime Detection**: Can detect if UnityExplorer is loaded via Assembly.GetAssembly() or MelonLoader's mod list
5. **Standalone Support**: Can be used standalone with `UnityExplorer.ExplorerStandalone.CreateInstance()`

**Practical Integration Approaches:**

**Approach 1: Reflection-Based Access (Recommended)**
- Use standard C# reflection APIs (same as UnityExplorer uses internally)
- Use Il2CppInterop for IL2CPP objects
- Implement GameObject finding/inspection using reflection
- No dependency on UnityExplorer, but uses same techniques
- **Pros**: No external dependency, full control
- **Cons**: Need to implement reflection utilities ourselves

**Approach 2: UnityExplorer as Reference Tool**
- Document UnityExplorer as recommended companion tool
- Users manually inspect objects in UnityExplorer UI
- MCP server provides object IDs/names that can be looked up
- **Pros**: Simple, no integration complexity
- **Cons**: Not programmatic, requires manual user interaction

**Approach 3: Optional UniverseLib Dependency (Recommended for Enhanced Features)**
- Use UniverseLib directly (UnityExplorer's foundation)
- **UniverseLib Repository**: https://github.com/yukieiji/UniverseLib (git submodule in UnityExplorer)
- Provides reflection utilities for both Mono and IL2CPP
- Provides GameObject finding, type checking, and cross-runtime compatibility
- Can detect if UnityExplorer is present for enhanced features
- **Pros**: Reuses proven reflection library, well-tested, cross-runtime support
- **Cons**: Adds dependency, may have version conflicts
- **Note**: UniverseLib is the foundation that UnityExplorer uses, so it's a stable dependency

**Approach 4: UnityExplorer InspectorManager API (Optional Integration)**
- Use UnityExplorer's `InspectorManager` API for programmatic object inspection
- `UnityExplorer.InspectorManager.Inspect(object)` - Programmatically inspect objects
- Requires UnityExplorer to be loaded as a mod
- **Pros**: Leverages UnityExplorer's robust inspection capabilities
- **Cons**: Requires UnityExplorer mod to be installed, adds dependency
- **Use Case**: When UnityExplorer is already installed, can leverage its inspection API

**Recommended Approach: Hybrid with UniverseLib**
- **Primary**: Use UniverseLib (Approach 3) for reflection utilities
  - Provides proven cross-runtime compatibility
  - Well-tested and maintained
  - Same foundation as UnityExplorer
- **Optional Enhancement**: Detect UnityExplorer presence
  - If UnityExplorer loaded: Can use `InspectorManager` API for advanced inspection
  - Can provide object references that match UnityExplorer's view
  - Fallback to UniverseLib if UnityExplorer not present
- **Companion Tool**: Document UnityExplorer as recommended for manual inspection
- **Implementation**: Use UniverseLib for core reflection, optionally integrate with UnityExplorer's InspectorManager

**Implementation Details for Reflection-Based Access:**

```csharp
// Example: GameObject finding (similar to UnityExplorer's approach)
public static class ReflectionHelper
{
    // Find GameObject by name (works in both Mono and IL2CPP)
    public static GameObject FindGameObject(string name)
    {
        #if MONO
        return GameObject.Find(name);
        #else
        // IL2CPP: Use Object.FindObjectOfType or Resources.FindObjectsOfTypeAll
        return Object.FindObjectOfType<GameObject>();
        #endif
    }
    
    // Get component via reflection (handles both Mono and IL2CPP)
    public static T GetComponent<T>(GameObject obj) where T : Component
    {
        #if MONO
        return obj.GetComponent<T>();
        #else
        // IL2CPP: Use Il2CppInterop for type casting
        return obj.TryCast<T>();
        #endif
    }
    
    // Access private fields via reflection
    public static object GetPrivateField(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, 
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(obj);
    }
}
```

**Key Implementation Points:**
1. **Cross-Backend Support**: Handle both Mono and IL2CPP reflection differences
2. **Il2CppInterop**: Use Il2CppInterop.Runtime for IL2CPP type casting
3. **BindingFlags**: Use reflection with proper flags for private/internal access
4. **Type Safety**: Validate types before casting to prevent crashes
5. **Error Handling**: Graceful degradation when objects not found

**UnityExplorer Use Cases for S1MCPServer:**
- Finding GameObjects by name when S1API doesn't expose them
- Inspecting component properties for debugging
- Accessing private/internal Unity fields via reflection
- Navigating complex GameObject hierarchies
- Discovering game object relationships
- Runtime object inspection and modification
- Validating object references match what users see in UnityExplorer UI

## S1API Reference and Custom Implementation

### Overview

S1MCPServer does **not** depend on S1API. Instead, we create our own custom implementation based on S1API patterns as a reference. This approach provides:

- **Independence**: No external API dependency
- **Flexibility**: Customize implementation for MCP server needs
- **Control**: Full control over API design and evolution
- **Reference**: S1API serves as a design pattern reference for cross-runtime compatibility

### S1API Research Findings

Based on research of S1API (https://ifbars.github.io/S1API/index.html), the following classes and patterns will guide our custom implementation:

#### 1. Cross-Runtime Compatibility Patterns

**S1API Approach:**
- Uses conditional compilation (`#if MONO` / `#elif IL2CPP`) for platform-specific code
- Provides abstraction layer between Mono and IL2CPP runtimes
- Handles namespace differences (`ScheduleOne.*` vs `Il2CppScheduleOne.*`)

**Our Implementation:**
- Follow same conditional compilation patterns
- Create utility classes for cross-runtime type checking and casting
- Use `Utils.Is<T>()` pattern (already implemented) for safe type checking

**Key Classes to Reference:**
- S1API's internal utilities for type conversion
- Cross-runtime compatibility helpers
- Namespace abstraction patterns

#### 2. NPC Management

**S1API Patterns (Reference):**
- NPC lookup and enumeration utilities
- NPC state access (position, health, consciousness)
- NPC relationship system access
- NPC scheduling system integration

**Our Custom Implementation:**
- Create `NPCManager` utility class
- Access native game NPC classes directly
- Implement NPC enumeration via reflection/Unity APIs
- Access NPC properties through native game object hierarchy

**Native Game Classes:**
- `ScheduleOne.*` (Mono) / `Il2CppScheduleOne.*` (IL2CPP) NPC classes
- Unity GameObject hierarchy for NPC instances
- Reflection-based property access

#### 3. Item Management

**S1API Patterns (Reference):**
- `ItemCreator` class for item creation
- `Registry.Instance.ItemRegistry` access
- Item definition management
- Item spawning utilities

**Our Custom Implementation:**
- Access `Registry.Instance.ItemRegistry` directly (native game class)
- Create custom item creation utilities
- Implement item spawning via native game APIs
- Use `StorableItemDefinition` from native game classes

**Native Game Classes:**
- `ScheduleOne.ItemFramework.Registry` (Mono)
- `Il2CppScheduleOne.ItemFramework.Registry` (IL2CPP)
- `StorableItemDefinition` class
- Item creation/spawning native methods

**Current Implementation:**
- Already using `Utils.GetAllStorableItemDefinitions()` which accesses `Registry.Instance.ItemRegistry`
- Pattern established for cross-runtime item access

#### 4. Building Management

**S1API Patterns (Reference):**
- `Building.GetAll()` - Enumerate all buildings
- `Building.Get<T>()` - Get building by type
- `Building.GetByName()` - Get building by name

**Our Custom Implementation:**
- Create `BuildingManager` utility class
- Access native game Building classes directly
- Implement building enumeration via native APIs
- Use reflection for building type access

**Native Game Classes:**
- `ScheduleOne.Buildings.*` (Mono) / `Il2CppScheduleOne.Buildings.*` (IL2CPP)
- Building enumeration methods
- Building type system

#### 5. Player Access

**S1API Patterns (Reference):**
- `Player.Local` access pattern
- Player state management
- Player inventory access
- Player relationship system

**Our Custom Implementation:**
- Use `Player.Local` directly (native game class)
- Access player properties via native game object
- Implement inventory access through native APIs
- Access player state through game's native systems

**Native Game Classes:**
- `ScheduleOne.PlayerScripts.Player` (Mono)
- `Il2CppScheduleOne.PlayerScripts.Player` (IL2CPP)
- Player inventory system
- Player state management

**Current Implementation:**
- Already using `Player.Local` in `PlayerCommandHandler`
- Pattern established for player access

#### 6. Vehicle Management

**S1API Patterns (Reference):**
- Vehicle enumeration utilities
- Vehicle state access
- Vehicle spawning utilities

**Our Custom Implementation:**
- Create `VehicleManager` utility class
- Access `VehicleManager.Instance.AllVehicles` (native game class)
- Implement vehicle enumeration via native APIs
- Access vehicle properties through native game objects

**Native Game Classes:**
- `ScheduleOne.*` VehicleManager (Mono)
- `Il2CppScheduleOne.*` VehicleManager (IL2CPP)
- Vehicle instance management

#### 7. Registry and Core Systems

**S1API Patterns (Reference):**
- `Registry.Instance` singleton pattern
- Item registry access
- Centralized game system access

**Our Custom Implementation:**
- Access `Registry.Instance` directly (native game singleton)
- Use native registry for item definitions
- Access other game registries as needed

**Native Game Classes:**
- `ScheduleOne.DevUtilities.Registry` (Mono)
- `Il2CppScheduleOne.DevUtilities.Registry` (IL2CPP)
- Registry singleton pattern

**Current Implementation:**
- Already using `Registry.Instance.ItemRegistry` in `Utils.GetAllStorableItemDefinitions()`

### Implementation Strategy

1. **Study S1API Patterns**: Review S1API source code and documentation to understand cross-runtime patterns
2. **Create Custom Utilities**: Build our own utility classes following S1API patterns but without dependency
3. **Direct Native Access**: Access Schedule One native classes directly with cross-runtime support
4. **Reflection-Based**: Use reflection for accessing game objects and properties
5. **Conditional Compilation**: Use `#if MONO` / `#elif IL2CPP` for runtime-specific code

### Key S1API Resources for Reference

- **S1API Documentation**: https://ifbars.github.io/S1API/index.html
- **S1API Installation Guide**: https://ifbars.github.io/S1API-docs/guide/installation.html
- **S1API Forked (Nexus Mods)**: https://www.nexusmods.com/schedule1/mods/1194
- **S1API Source Code**: Study GitHub repository for implementation patterns

### UniverseLib Integration

**UniverseLib** is the recommended dependency for reflection utilities in S1MCPServer:

- **Repository**: https://github.com/yukieiji/UniverseLib
- **Purpose**: Cross-runtime reflection utilities for Mono and IL2CPP
- **Relationship**: Foundation library used by UnityExplorer
- **Benefits**:
  - Well-tested and maintained
  - Cross-runtime compatibility (Mono/IL2CPP)
  - GameObject finding and inspection utilities
  - Type checking and casting utilities
  - Same foundation as UnityExplorer (proven reliability)

**Integration Approach:**
1. Add UniverseLib as a project dependency (reference the DLL or NuGet package if available)
2. Use UniverseLib utilities for GameObject finding, type checking, and reflection
3. Implement custom utilities on top of UniverseLib for game-specific needs
4. Optional: Detect and integrate with UnityExplorer's InspectorManager if UnityExplorer is loaded

**UniverseLib Key Features:**
- Cross-runtime type checking and casting
- GameObject finding and enumeration
- Component inspection
- Property and field access via reflection
- IL2CPP interop utilities

### UnityExplorer Resources

- **UnityExplorer Repository**: https://github.com/yukieiji/UnityExplorer/
- **UnityExplorer InspectorManager API**: 
  - `UnityExplorer.InspectorManager.Inspect(object)` - Programmatically inspect objects
  - `UnityExplorer.InspectorManager.Inspect(Type)` - Inspect types
- **Standalone Usage**: Can be used standalone with `UnityExplorer.ExplorerStandalone.CreateInstance()`
- **Compatibility**: Supports Unity 5.2 to 2021+ (IL2CPP and Mono)

### Custom Utility Classes to Implement

Based on S1API patterns and using UniverseLib for reflection, we'll create:

1. **NPCManager** - NPC enumeration, lookup, and state access
   - Uses UniverseLib for GameObject finding and type checking
2. **BuildingManager** - Building enumeration and access
   - Uses UniverseLib for reflection and type access
3. **VehicleManager** - Vehicle enumeration and access
   - Uses UniverseLib for GameObject finding
4. **ItemManager** - Item creation, spawning, and management (extends current Utils)
   - Uses UniverseLib for type checking and casting
5. **PlayerManager** - Player state and inventory access (extends current PlayerCommandHandler)
   - Uses UniverseLib for component access
6. **ReflectionHelper** - Enhanced reflection utilities (extends UniverseLib)
   - Built on UniverseLib foundation
   - Adds game-specific reflection utilities
   - Optional UnityExplorer InspectorManager integration

### Notes

- S1API is a **reference only** - we do not include it as a dependency
- All implementations use native Schedule One game classes directly
- Cross-runtime compatibility patterns from S1API guide our implementation
- We maintain independence while learning from S1API's proven patterns

## MCP Server Design

### MCP Tools (Functions Exposed to LLM)

1. **`s1_get_npc`** - Get NPC information
   - Parameters: `npc_id` (string)
   - Returns: NPC state object

2. **`s1_list_npcs`** - List all NPCs
   - Parameters: `filter` (optional, e.g., "conscious", "in_building")
   - Returns: Array of NPC summaries

3. **`s1_get_npc_position`** - Get NPC position
   - Parameters: `npc_id` (string)
   - Returns: Position object {x, y, z}

4. **`s1_teleport_npc`** - Teleport NPC
   - Parameters: `npc_id` (string), `position` (object)
   - Returns: Success status

5. **`s1_get_player`** - Get player information
   - Parameters: None
   - Returns: Player state object

6. **`s1_get_player_inventory`** - Get player inventory
   - Parameters: None
   - Returns: Array of items

7. **`s1_add_item_to_player`** - Add item to player inventory
   - Parameters: `item_id` (string), `quantity` (number, optional)
   - Returns: Success status

8. **`s1_list_items`** - List all item definitions
   - Parameters: `category` (optional, string)
   - Returns: Array of item definitions

9. **`s1_get_building`** - Get building information
   - Parameters: `building_name` (string)
   - Returns: Building state object

10. **`s1_list_buildings`** - List all buildings
    - Parameters: None
    - Returns: Array of building summaries

11. **`s1_inspect_object`** - Inspect game object properties
    - Parameters: `object_id` (string), `object_type` (string), `use_unityexplorer` (optional, boolean)
    - Returns: Object properties
    - **Note**: Can optionally leverage UnityExplorer for deep Unity object inspection

12. **`s1_execute_command`** - Execute game command (advanced)
    - Parameters: `command` (string), `parameters` (object)
    - Returns: Command result
    - **Note**: Should have safety restrictions

### MCP Resources (Data Exposed to LLM)

1. **Game State Snapshot** - Current game state summary
2. **Mod List** - List of loaded mods
3. **Custom API Documentation** - Reference for available APIs (based on S1API patterns)

## Implementation Details

### Mod Side (C# / MelonLoader)

**Components:**

1. **NamedPipeServer** (`S1MCPServer.Server.NamedPipeServer`)
   - Background thread listener
   - Handles pipe connections
   - Deserializes JSON requests
   - Enqueues commands

2. **CommandQueue** (`S1MCPServer.Core.CommandQueue`)
   - Thread-safe queue
   - Stores pending game operations
   - Processes on main thread

3. **GameAPI** (`S1MCPServer.API.GameAPI`)
   - Main thread executor
   - Accesses Unity/native game objects via custom utilities
   - Executes game operations
   - Returns results
   - Optional UnityExplorer integration for advanced GameObject access
   - Custom implementation based on S1API patterns (not S1API dependency)

4. **ResponseQueue** (`S1MCPServer.Core.ResponseQueue`)
   - Thread-safe response queue
   - Stores operation results
   - Sends via named pipe

5. **Command Handlers** (`S1MCPServer.Handlers.*`)
   - `NPCCommandHandler` - NPC operations
   - `ItemCommandHandler` - Item operations
   - `PlayerCommandHandler` - Player operations
   - `BuildingCommandHandler` - Building operations
   - `VehicleCommandHandler` - Vehicle operations
   - `DebugCommandHandler` - Debug operations
   - `UnityExplorerHandler` - Optional UnityExplorer integration for GameObject access

6. **Reflection Utilities** (`S1MCPServer.Utils.ReflectionHelper`)
   - Built on UniverseLib for cross-runtime reflection
   - GameObject finding by name/path
   - Component inspection via reflection
   - Property/field access (public and private)
   - Il2CppInterop integration for IL2CPP objects
   - Cross-backend compatibility (Mono/IL2CPP)
   - Uses UniverseLib utilities (same foundation as UnityExplorer)

7. **UnityExplorer Integration** (`S1MCPServer.Integrations.UnityExplorer`)
   - Optional integration with UnityExplorer's `InspectorManager` API
   - Detects if UnityExplorer is loaded
   - Uses `UnityExplorer.InspectorManager.Inspect()` for advanced inspection
   - Falls back to UniverseLib if UnityExplorer not present
   - **Repository**: https://github.com/yukieiji/UnityExplorer/

**Threading Model:**
```
Background Thread (Named Pipe Server)
    ↓ Enqueue
Command Queue (Thread-Safe)
    ↓ Process on Main Thread (Unity Update/LateUpdate)
Game API (Main Thread)
    ↓ Enqueue Result
Response Queue (Thread-Safe)
    ↓ Send via Pipe
Background Thread (Named Pipe Server)
```

### MCP Server Side (Language TBD)

**Responsibilities:**
- Implement MCP protocol (JSON-RPC over stdio)
- Translate MCP tools to game API calls
- Manage connection to mod via named pipe
- Handle LLM interactions
- Provide error handling and logging

**Language Options:**
- **Python**: Good MCP library support, easy to develop
- **Node.js**: Good async support, TypeScript for type safety
- **C#**: Same language as mod, easier to share code/types

## Security Considerations

### Safety Measures

1. **Read-Only by Default**: Most operations should be read-only unless explicitly needed
2. **Operation Validation**: Validate all parameters before execution
3. **Rate Limiting**: Prevent excessive API calls that could impact game performance
4. **Sandboxing**: If code execution is enabled, use sandboxing
5. **Error Handling**: Graceful error handling to prevent game crashes
6. **Localhost Only**: Named pipes are local-only by default (good security)

### Dangerous Operations

Operations that require extra caution:
- Modifying save files
- Executing arbitrary C# code
- Deleting game objects
- Modifying critical game state

**Mitigation:**
- Require explicit confirmation flags
- Log all write operations
- Provide rollback mechanisms where possible
- Limit scope of code execution

## Use Cases

### 1. Mod Debugging
**Scenario**: A mod isn't working as expected
- LLM can inspect game state
- Check if NPCs are spawning correctly
- Verify item registration
- Inspect mod interactions
- Suggest fixes based on state

### 2. Game State Inspection
**Scenario**: Understanding current game state
- List all NPCs and their states
- Check player inventory
- Inspect building occupancy
- Review relationships

### 3. Testing Scenarios
**Scenario**: Creating test scenarios
- Spawn NPCs at specific locations
- Add items to inventory
- Modify relationships
- Trigger events

### 4. Development Assistance
**Scenario**: Developing new mods
- Inspect available game objects
- Test S1API functionality
- Verify mod integration
- Debug mod behavior

## Performance Considerations

### Resource Usage
- **Memory**: ~2-5MB for queues and buffers
- **CPU**: Minimal when idle, spikes during operations
- **Threads**: 1 background thread for pipe handling
- **Latency**: ~16-33ms (1 frame) for main thread operations

### Optimization Strategies
1. **Batch Operations**: Group multiple operations when possible
2. **Lazy Loading**: Don't load all game objects at once
3. **Caching**: Cache frequently accessed data
4. **Async Where Possible**: Use async/await for I/O operations

## Development Roadmap

### Phase 1: Core Infrastructure
- [ ] Named pipe server implementation
- [ ] Command/response queue system
- [ ] Basic protocol implementation
- [ ] Thread-safe communication
- [ ] Integrate UniverseLib dependency for reflection utilities
- [ ] Reflection utilities for GameObject access (using UniverseLib)

### Phase 2: Basic Game Access
- [ ] NPC read operations
- [ ] Player read operations
- [ ] Item read operations
- [ ] Building read operations

### Phase 3: Write Operations
- [ ] NPC modification operations
- [ ] Player modification operations
- [ ] Item spawning/management
- [ ] Safety validation

### Phase 4: MCP Server
- [ ] MCP protocol implementation
- [ ] Tool definitions
- [ ] Resource definitions
- [ ] Error handling

### Phase 5: Advanced Features
- [ ] Code execution (sandboxed)
- [ ] Advanced debugging tools
- [ ] Enhanced reflection utilities (using UniverseLib)
- [ ] Optional UnityExplorer InspectorManager API integration
- [ ] Performance monitoring
- [ ] Documentation and examples

## Technical Requirements

### Mod Side
- .NET 6.0 (for IL2CPP) or .NET Standard 2.1 (for Mono)
- MelonLoader compatibility
- **No S1API dependency** - Custom implementation based on S1API patterns as reference
- Cross-backend support (IL2CPP and Mono)
- Il2CppInterop (for IL2CPP reflection)
- System.Reflection (for Mono reflection)
- **UniverseLib** (recommended dependency for reflection utilities)
  - Repository: https://github.com/yukieiji/UniverseLib
  - Provides cross-runtime reflection utilities
  - Same foundation as UnityExplorer
  - Well-tested and maintained
- **UnityExplorer** (optional companion tool and optional API integration)
  - Repository: https://github.com/yukieiji/UnityExplorer/
  - Can use `InspectorManager` API if UnityExplorer is loaded
  - Recommended for manual inspection
- Direct access to Schedule One native classes (ScheduleOne.* / Il2CppScheduleOne.*)

### MCP Server Side
- Language runtime (Python 3.10+, Node.js 18+, or .NET 6+)
- MCP SDK/library
- JSON serialization library
- Named pipe client library

## Open Questions

1. **MCP Server Language**: Which language should we use? (Python recommended for ecosystem)
2. **Code Execution**: Should we support executing C# code snippets? (High risk, high reward)
3. **Authentication**: Do we need authentication for localhost communication? (Probably not needed)
4. **Multiplayer Support**: How should we handle multiplayer scenarios? (Read-only for clients?)
5. **Versioning**: How do we handle API versioning? (JSON-RPC version field)
6. **UnityExplorer Integration**: 
   - **Repository**: https://github.com/yukieiji/UnityExplorer/
   - **Decision**: Use UniverseLib as primary dependency (UnityExplorer's foundation)
   - **Optional**: Integrate with UnityExplorer's `InspectorManager` API if UnityExplorer is loaded
   - UnityExplorer documented as optional companion tool for manual inspection
   - **UniverseLib Dependency**: Recommended for cross-runtime reflection utilities

## Success Criteria

1. ✅ LLM can successfully inspect game state
2. ✅ LLM can diagnose common mod issues
3. ✅ Operations complete within 1-2 frames
4. ✅ No significant performance impact on game
5. ✅ Safe error handling prevents game crashes
6. ✅ Documentation enables community adoption

## Next Steps

1. **Finalize Architecture**: Confirm Named Pipe + Command Queue approach
2. **Define Protocol**: Specify exact JSON-RPC message format
3. **Choose MCP Server Language**: Decide on implementation language
4. **Create API Specification**: Document all available operations
5. **Begin Implementation**: Start with Phase 1 (Core Infrastructure)

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-27  
**Author**: Tyler (with AI assistance)

