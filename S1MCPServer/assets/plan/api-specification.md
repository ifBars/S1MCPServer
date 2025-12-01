# S1MCPServer API Specification

## Overview

This document defines the complete API specification for S1MCPServer, including the Named Pipe communication protocol, command structure, and all available operations.

## Named Pipes Communication Protocol

### What are Named Pipes?

Named Pipes are a Windows inter-process communication (IPC) mechanism that allows two processes on the same machine to communicate. Think of them like a file that both processes can read from and write to, but it's actually a communication channel in memory.

**Key Concepts:**
- **Server**: Creates the pipe and waits for connections (our mod)
- **Client**: Connects to the pipe and sends/receives data (MCP server)
- **One-way or Duplex**: We'll use duplex (bidirectional) communication
- **Blocking**: Operations wait until data is available or written
- **Thread-safe**: Can be used from multiple threads with proper synchronization

### Named Pipe Setup

**Pipe Name**: `\\.\pipe\S1MCPServer`
- `\\.\pipe\` is the Windows prefix for named pipes
- `S1MCPServer` is our unique pipe name

**Server Side (Mod)**:
```csharp
// Create named pipe server
NamedPipeServerStream pipeServer = new NamedPipeServerStream(
    "S1MCPServer",                    // Pipe name
    PipeDirection.InOut,              // Bidirectional communication
    1,                                // Max instances (only 1 client at a time)
    PipeTransmissionMode.Byte,        // Byte mode (not message mode)
    PipeOptions.Asynchronous          // Non-blocking operations
);
```

**Client Side (MCP Server)**:
```python
# Python example
import win32pipe, win32file

pipe_handle = win32pipe.CreateFile(
    r'\\.\pipe\S1MCPServer',
    win32file.GENERIC_READ | win32file.GENERIC_WRITE,
    0, None,
    win32file.OPEN_EXISTING,
    0, None
)
```

### Message Protocol

**Format**: JSON-RPC 2.0-like protocol over Named Pipe

**Message Structure**:
1. **Length Prefix**: 4 bytes (little-endian int32) indicating JSON message length
2. **JSON Payload**: UTF-8 encoded JSON string

**Example Message Flow**:
```
[4 bytes: length] [JSON string]
    0x0000002F     {"id":1,"method":"get_npc","params":{"npc_id":"kyle_cooley"}}
```

### Request Format

```json
{
  "id": 1,                              // Unique request ID (integer)
  "method": "get_npc",                  // Command name (string)
  "params": {                           // Parameters object
    "npc_id": "kyle_cooley"             // Method-specific parameters
  }
}
```

### Response Format

**Success Response**:
```json
{
  "id": 1,                              // Matches request ID
  "result": {                           // Result object
    "npc_id": "kyle_cooley",
    "name": "Kyle Cooley",
    "position": {"x": 10.5, "y": 1.0, "z": 20.3},
    "health": 100.0,
    "is_conscious": true
  },
  "error": null                         // null on success
}
```

**Error Response**:
```json
{
  "id": 1,                              // Matches request ID
  "result": null,                       // null on error
  "error": {
    "code": -32602,                     // Error code (JSON-RPC standard)
    "message": "Invalid parameter",     // Human-readable message
    "data": {                           // Additional error data
      "parameter": "npc_id",
      "reason": "NPC not found"
    }
  }
}
```

### Error Codes

| Code | Name | Description |
|------|------|-------------|
| -32700 | Parse error | Invalid JSON was received |
| -32600 | Invalid Request | The JSON sent is not a valid Request object |
| -32601 | Method not found | The method does not exist / is not available |
| -32602 | Invalid params | Invalid method parameter(s) |
| -32603 | Internal error | Internal JSON-RPC error |
| -32000 | Game error | Game-specific error (e.g., object not found) |
| -32001 | Thread error | Operation failed due to threading issue |
| -32002 | Validation error | Parameter validation failed |

## Command API Reference

### NPC Commands

#### `get_npc`
Get detailed information about a specific NPC.

**Request**:
```json
{
  "id": 1,
  "method": "get_npc",
  "params": {
    "npc_id": "kyle_cooley"
  }
}
```

**Response**:
```json
{
  "id": 1,
  "result": {
    "npc_id": "kyle_cooley",
    "name": "Kyle Cooley",
    "position": {
      "x": -28.06,
      "y": 1.065,
      "z": 62.07
    },
    "rotation": {
      "x": 0.0,
      "y": 90.0,
      "z": 0.0
    },
    "health": 100.0,
    "max_health": 100.0,
    "is_conscious": true,
    "is_panicking": false,
    "is_unsettled": false,
    "is_visible": true,
    "is_in_building": false,
    "is_in_vehicle": false,
    "region": "Northtown",
    "aggressiveness": 3.0,
    "relationships": [
      {
        "npc_id": "ludwig_meyer",
        "relationship_value": 0.5
      }
    ]
  },
  "error": null
}
```

**Error Cases**:
- `npc_id` not found: Error code -32000, message "NPC not found"

---

#### `list_npcs`
List all NPCs, optionally filtered.

**Request**:
```json
{
  "id": 2,
  "method": "list_npcs",
  "params": {
    "filter": "conscious"  // Optional: "conscious", "unconscious", "in_building", "in_vehicle", null for all
  }
}
```

**Response**:
```json
{
  "id": 2,
  "result": {
    "npcs": [
      {
        "npc_id": "kyle_cooley",
        "name": "Kyle Cooley",
        "position": {"x": -28.06, "y": 1.065, "z": 62.07},
        "is_conscious": true,
        "health": 100.0
      },
      {
        "npc_id": "ludwig_meyer",
        "name": "Ludwig Meyer",
        "position": {"x": -30.0, "y": 1.0, "z": 65.0},
        "is_conscious": true,
        "health": 85.0
      }
    ],
    "count": 2
  },
  "error": null
}
```

---

#### `get_npc_position`
Get NPC's current position.

**Request**:
```json
{
  "id": 3,
  "method": "get_npc_position",
  "params": {
    "npc_id": "kyle_cooley"
  }
}
```

**Response**:
```json
{
  "id": 3,
  "result": {
    "npc_id": "kyle_cooley",
    "position": {
      "x": -28.06,
      "y": 1.065,
      "z": 62.07
    }
  },
  "error": null
}
```

---

#### `teleport_npc`
Teleport an NPC to a specific position.

**Request**:
```json
{
  "id": 4,
  "method": "teleport_npc",
  "params": {
    "npc_id": "kyle_cooley",
    "position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    }
  }
}
```

**Response**:
```json
{
  "id": 4,
  "result": {
    "success": true,
    "npc_id": "kyle_cooley",
    "new_position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    }
  },
  "error": null
}
```

**Error Cases**:
- Invalid position (e.g., underground): Error code -32002, message "Invalid position"

---

#### `set_npc_health`
Modify NPC's health.

**Request**:
```json
{
  "id": 5,
  "method": "set_npc_health",
  "params": {
    "npc_id": "kyle_cooley",
    "health": 50.0
  }
}
```

**Response**:
```json
{
  "id": 5,
  "result": {
    "success": true,
    "npc_id": "kyle_cooley",
    "old_health": 100.0,
    "new_health": 50.0
  },
  "error": null
}
```

---

### Player Commands

#### `get_player`
Get current player information.

**Request**:
```json
{
  "id": 6,
  "method": "get_player",
  "params": {}
}
```

**Response**:
```json
{
  "id": 6,
  "result": {
    "position": {
      "x": 10.5,
      "y": 1.0,
      "z": 20.3
    },
    "rotation": {
      "x": 0.0,
      "y": 45.0,
      "z": 0.0
    },
    "health": 100.0,
    "money": 5000.0,
    "network_status": "host"  // "host", "client", "singleplayer"
  },
  "error": null
}
```

---

#### `get_player_inventory`
Get player's inventory items.

**Request**:
```json
{
  "id": 7,
  "method": "get_player_inventory",
  "params": {}
}
```

**Response**:
```json
{
  "id": 7,
  "result": {
    "items": [
      {
        "item_id": "cuke",
        "name": "Cucumber",
        "quantity": 5,
        "stack_limit": 10
      },
      {
        "item_id": "knife",
        "name": "Knife",
        "quantity": 1,
        "stack_limit": 1
      }
    ],
    "count": 2
  },
  "error": null
}
```

---

#### `add_item_to_player`
Add item(s) to player inventory.

**Request**:
```json
{
  "id": 8,
  "method": "add_item_to_player",
  "params": {
    "item_id": "cuke",
    "quantity": 3
  }
}
```

**Response**:
```json
{
  "id": 8,
  "result": {
    "success": true,
    "item_id": "cuke",
    "quantity_added": 3,
    "new_total": 8
  },
  "error": null
}
```

**Error Cases**:
- Item not found: Error code -32000, message "Item not found"
- Inventory full: Error code -32000, message "Inventory full"

---

#### `teleport_player`
Teleport player to a position.

**Request**:
```json
{
  "id": 9,
  "method": "teleport_player",
  "params": {
    "position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    }
  }
}
```

**Response**:
```json
{
  "id": 9,
  "result": {
    "success": true,
    "new_position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    }
  },
  "error": null
}
```

---

### Item Commands

#### `list_items`
List all item definitions in the game.

**Request**:
```json
{
  "id": 10,
  "method": "list_items",
  "params": {
    "category": null  // Optional: filter by category (string) or null for all
  }
}
```

**Response**:
```json
{
  "id": 10,
  "result": {
    "items": [
      {
        "item_id": "cuke",
        "name": "Cucumber",
        "description": "A fresh cucumber",
        "category": "Consumable",
        "base_price": 10.0,
        "stack_limit": 10
      },
      {
        "item_id": "knife",
        "name": "Knife",
        "description": "A sharp knife",
        "category": "Tools",
        "base_price": 50.0,
        "stack_limit": 1
      }
    ],
    "count": 2
  },
  "error": null
}
```

---

#### `get_item`
Get detailed information about an item definition.

**Request**:
```json
{
  "id": 11,
  "method": "get_item",
  "params": {
    "item_id": "cuke"
  }
}
```

**Response**:
```json
{
  "id": 11,
  "result": {
    "item_id": "cuke",
    "name": "Cucumber",
    "description": "A fresh cucumber",
    "category": "Consumable",
    "base_price": 10.0,
    "stack_limit": 10,
    "legal_status": "legal"
  },
  "error": null
}
```

---

#### `spawn_item`
Spawn an item in the world at a specific position.

**Request**:
```json
{
  "id": 12,
  "method": "spawn_item",
  "params": {
    "item_id": "cuke",
    "position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    },
    "quantity": 1
  }
}
```

**Response**:
```json
{
  "id": 12,
  "result": {
    "success": true,
    "item_id": "cuke",
    "quantity": 1,
    "position": {
      "x": 0.0,
      "y": 1.0,
      "z": 0.0
    }
  },
  "error": null
}
```

---

### Building Commands

#### `list_buildings`
List all buildings in the game.

**Request**:
```json
{
  "id": 13,
  "method": "list_buildings",
  "params": {}
}
```

**Response**:
```json
{
  "id": 13,
  "result": {
    "buildings": [
      {
        "building_id": "north_apartments",
        "name": "North Apartments",
        "position": {
          "x": -28.06,
          "y": 1.065,
          "z": 62.07
        }
      }
    ],
    "count": 1
  },
  "error": null
}
```

---

#### `get_building`
Get detailed information about a building.

**Request**:
```json
{
  "id": 14,
  "method": "get_building",
  "params": {
    "building_id": "north_apartments"  // or "building_name": "North Apartments"
  }
}
```

**Response**:
```json
{
  "id": 14,
  "result": {
    "building_id": "north_apartments",
    "name": "North Apartments",
    "position": {
      "x": -28.06,
      "y": 1.065,
      "z": 62.07
    },
    "npcs_inside": [
      {
        "npc_id": "kyle_cooley",
        "name": "Kyle Cooley"
      }
    ],
    "npc_count": 1
  },
  "error": null
}
```

---

### Vehicle Commands

#### `list_vehicles`
List all vehicles in the game.

**Request**:
```json
{
  "id": 15,
  "method": "list_vehicles",
  "params": {}
}
```

**Response**:
```json
{
  "id": 15,
  "result": {
    "vehicles": [
      {
        "vehicle_id": "vehicle_001",
        "vehicle_type": "car",
        "position": {
          "x": 10.0,
          "y": 1.0,
          "z": 20.0
        },
        "occupants": []
      }
    ],
    "count": 1
  },
  "error": null
}
```

---

### Game State Commands

#### `get_game_state`
Get current game state information.

**Request**:
```json
{
  "id": 16,
  "method": "get_game_state",
  "params": {}
}
```

**Response**:
```json
{
  "id": 16,
  "result": {
    "scene_name": "Main",
    "game_time": 1200,  // In-game time (minutes since midnight)
    "network_status": "host",
    "game_version": "1.0.0",
    "loaded_mods": [
      "S1MCPServer",
      "ExampleMod"
    ]
  },
  "error": null
}
```

---

### Debug Commands

#### `inspect_object`
Inspect a Unity GameObject or component using reflection.

**Request**:
```json
{
  "id": 17,
  "method": "inspect_object",
  "params": {
    "object_name": "SomeGameObject",  // GameObject name
    "object_type": "GameObject"        // "GameObject", "Component", or component type name
  }
}
```

**Response**:
```json
{
  "id": 17,
  "result": {
    "object_name": "SomeGameObject",
    "object_type": "GameObject",
    "components": [
      {
        "type": "Transform",
        "properties": {
          "position": {"x": 0.0, "y": 0.0, "z": 0.0},
          "rotation": {"x": 0.0, "y": 0.0, "z": 0.0},
          "scale": {"x": 1.0, "y": 1.0, "z": 1.0}
        }
      }
    ],
    "children": []
  },
  "error": null
}
```

---

## Implementation Guide

### Named Pipe Server Implementation (Mod Side)

**Step 1: Create Pipe Server**
```csharp
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

public class NamedPipeServer
{
    private NamedPipeServerStream _pipeServer;
    private bool _isRunning = false;
    private readonly CommandQueue _commandQueue;
    
    public NamedPipeServer(CommandQueue commandQueue)
    {
        _commandQueue = commandQueue;
    }
    
    public void Start()
    {
        _isRunning = true;
        Task.Run(() => ServerLoop());
    }
    
    private async void ServerLoop()
    {
        while (_isRunning)
        {
            try
            {
                // Create and wait for connection
                _pipeServer = new NamedPipeServerStream(
                    "S1MCPServer",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );
                
                await _pipeServer.WaitForConnectionAsync();
                Logger.Msg("Client connected to named pipe");
                
                // Handle client communication
                await HandleClient();
            }
            catch (Exception ex)
            {
                Logger.Error($"Pipe server error: {ex.Message}");
            }
            finally
            {
                _pipeServer?.Dispose();
            }
        }
    }
}
```

**Step 2: Read Messages**
```csharp
private async Task HandleClient()
{
    byte[] lengthBuffer = new byte[4];
    
    while (_pipeServer.IsConnected)
    {
        try
        {
            // Read 4-byte length prefix
            int bytesRead = await _pipeServer.ReadAsync(lengthBuffer, 0, 4);
            if (bytesRead != 4) break;
            
            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
            
            // Read JSON message
            byte[] messageBuffer = new byte[messageLength];
            bytesRead = await _pipeServer.ReadAsync(messageBuffer, 0, messageLength);
            if (bytesRead != messageLength) break;
            
            string jsonMessage = Encoding.UTF8.GetString(messageBuffer);
            
            // Parse and enqueue command
            var request = JsonSerializer.Deserialize<Request>(jsonMessage);
            _commandQueue.EnqueueCommand(request);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error reading from pipe: {ex.Message}");
            break;
        }
    }
}
```

**Step 3: Write Responses**
```csharp
private void SendResponse(Response response)
{
    try
    {
        string jsonResponse = JsonSerializer.Serialize(response);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonResponse);
        byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
        
        // Write length prefix
        _pipeServer.Write(lengthBytes, 0, 4);
        
        // Write JSON message
        _pipeServer.Write(jsonBytes, 0, jsonBytes.Length);
        _pipeServer.Flush();
    }
    catch (Exception ex)
    {
        Logger.Error($"Error writing to pipe: {ex.Message}");
    }
}
```

### Command Processing Flow

```
1. Named Pipe Server receives request (background thread)
   ↓
2. Deserialize JSON to Request object
   ↓
3. Enqueue to CommandQueue (thread-safe)
   ↓
4. Main thread processes command (Unity Update/LateUpdate)
   ↓
5. Execute game operation via GameAPI
   ↓
6. Enqueue result to ResponseQueue
   ↓
7. Named Pipe Server sends response (background thread)
```

## Next Steps

1. **Implement Named Pipe Server**: Create the server class
2. **Implement Command Queue**: Thread-safe queue system
3. **Implement Command Handlers**: One handler per command category
4. **Implement Response Queue**: Thread-safe response handling
5. **Test Communication**: Verify end-to-end message flow

---

**Document Version**: 1.0  
**Last Updated**: 2025-01-27

