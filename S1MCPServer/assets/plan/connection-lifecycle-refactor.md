# Connection Lifecycle Refactor

## Date
December 2, 2025

## Problem Statement

The MCP server was attempting to connect to the game immediately at startup, which caused several issues:

1. **Wrong Startup Sequence**: MCP server tried to connect before the game was running
2. **No Connection Guards**: Game tools could be called even when not connected, causing errors
3. **Poor Game Launch Flow**: `s1_launch_game` didn't properly establish connection after starting game
4. **Lost Instructions**: Handshake instructions from game server weren't being received/stored properly
5. **No State Tracking**: No way to know if game was connected or not

## Solution Overview

Refactored the connection lifecycle so the MCP server:
- Starts independently without requiring game connection
- Uses `s1_launch_game` to start and connect to the game
- Guards all game tools to prevent calls when not connected
- Properly tracks connection state globally
- Receives and stores handshake instructions

## Changes Made

### 1. Connection State Management (`main.py`)

**Added global connection state:**
```python
# Track connection state
is_connected: bool = False
```

**Added connection guard function:**
```python
# Lifecycle tools that don't require game connection
LIFECYCLE_TOOLS = {"s1_launch_game", "s1_close_game", "s1_get_game_process_info"}

def can_call_tool(tool_name: str) -> tuple[bool, str]:
    """Check if a tool can be called based on connection state."""
    global is_connected
    
    # Lifecycle tools can always be called
    if tool_name in LIFECYCLE_TOOLS:
        return True, ""
    
    # All other tools require game connection
    if not is_connected:
        return False, (
            "Error: Game is not connected. Please launch the game first using s1_launch_game.\n"
            "Once the game is running and connected, you can use other game tools."
        )
    
    return True, ""
```

### 2. Optional Startup Connection (`main.py`)

**Changed connection attempt from required to optional:**
- Connection failures at startup no longer cause errors
- MCP server starts successfully even without game running
- Logs informative message about waiting for game launch

**Before:**
```python
# Try to connect (will retry on first use if it fails)
try:
    tcp_client.connect()
    # ... handshake ...
except TcpConnectionError as e:
    logger.warning(f"Initial connection failed: {e}. Will retry on first tool call.")
```

**After:**
```python
# Try to connect (optional - game might not be running yet)
global is_connected
try:
    tcp_client.connect()
    # ... handshake ...
    is_connected = True  # Mark as connected on success
except TcpConnectionError as e:
    logger.info(f"Game not running at startup: {e}")
    logger.info("MCP server will wait for game to be launched via s1_launch_game tool.")
    is_connected = False
```

### 3. Tool Call Guards (`main.py`)

**Added connection check in tool handler:**
```python
# Check if tool can be called based on connection state
can_call, error_msg = can_call_tool(name)
if not can_call:
    logger.warning(f"Tool {name} called but game not connected")
    return [TextContent(type="text", text=error_msg)]
```

Now all game tools (NPCs, player, items, etc.) are blocked until game is connected.
Only lifecycle tools (`s1_launch_game`, `s1_close_game`, `s1_get_game_process_info`) work when disconnected.

### 4. Enhanced `s1_launch_game` (`game_lifecycle_tools.py`)

**Added connection state updates after successful connection:**
```python
# Update global connection state in main module
try:
    import sys
    main_module = sys.modules.get('__main__') or sys.modules.get('s1mcpclient.src.main')
    if main_module and hasattr(main_module, 'is_connected'):
        main_module.is_connected = True
        logger.info("Updated global connection state to connected")
    
    # Store instructions if available
    if "instructions" in server_info and hasattr(main_module, 'server_instructions'):
        main_module.server_instructions = server_info["instructions"]
        logger.info(f"Received and stored server instructions ({len(server_info['instructions'])} chars)")
except Exception as e:
    logger.warning(f"Could not update global connection state: {e}")
```

**Added handshake debugging:**
```python
# Log handshake response details for debugging
if isinstance(response.result, dict):
    logger.debug(f"Handshake result keys: {response.result.keys()}")
    if "instructions" in response.result:
        logger.info(f"Received instructions: {len(response.result['instructions'])} characters")
    else:
        logger.warning("No 'instructions' key in handshake response!")
else:
    logger.warning(f"Handshake result is not a dict: {type(response.result)}")
```

### 5. Enhanced `s1_close_game` (`game_lifecycle_tools.py`)

**Added connection state reset when game closes:**
```python
# Update global connection state
try:
    import sys
    main_module = sys.modules.get('__main__') or sys.modules.get('s1mcpclient.src.main')
    if main_module and hasattr(main_module, 'is_connected'):
        main_module.is_connected = False
        logger.info("Updated global connection state to disconnected")
except Exception as e:
    logger.warning(f"Could not update global connection state: {e}")
```

## New Workflow

### Correct Usage Flow:

1. **Start MCP Server** (game not required)
   ```
   MCP server starts → No game connection → Ready to receive commands
   ```

2. **Launch Game via Tool**
   ```
   Agent calls s1_launch_game → Game starts → Wait for TCP server (~30s) → Connect → Handshake → Store instructions → Update connection state
   ```

3. **Use Game Tools**
   ```
   Agent calls game tools (s1_get_player, s1_list_npcs, etc.) → Connection guard passes → Tools work
   ```

4. **Close Game via Tool**
   ```
   Agent calls s1_close_game → Disconnect TCP → Kill process → Reset connection state
   ```

### Tool Availability by State:

| Tool | When Disconnected | When Connected |
|------|------------------|----------------|
| `s1_launch_game` | ✅ Available | ✅ Available |
| `s1_close_game` | ✅ Available | ✅ Available |
| `s1_get_game_process_info` | ✅ Available | ✅ Available |
| All other game tools | ❌ Blocked | ✅ Available |

## Configuration

The following config values control connection timing:

```json
{
  "game_startup_timeout": 60.0,           // Max wait for game connection (seconds)
  "game_connection_poll_interval": 2.0    // Time between connection attempts (seconds)
}
```

Default values are appropriate for the ~30 second game startup time.

## Testing Instructions

To test the new connection lifecycle:

1. **Start MCP server with game closed:**
   ```bash
   cd S1MCPClient
   python -m src.main
   ```
   - Should start successfully
   - Should log: "Game not running at startup"
   - Should NOT error or exit

2. **Try calling a game tool (should fail):**
   ```
   s1_get_player
   ```
   - Should return error about game not connected
   - Should suggest using s1_launch_game

3. **Launch game:**
   ```
   s1_launch_game(version="il2cpp", wait_for_connection=true)
   ```
   - Should start game process
   - Should wait up to 60 seconds for connection
   - Should perform handshake
   - Should log: "Received instructions: X characters"
   - Should log: "Updated global connection state to connected"

4. **Try calling game tools (should work):**
   ```
   s1_get_player
   s1_list_npcs
   ```
   - Should work normally now

5. **Close game:**
   ```
   s1_close_game
   ```
   - Should close game
   - Should log: "Updated global connection state to disconnected"

6. **Try calling game tools again (should fail):**
   ```
   s1_get_player
   ```
   - Should return error about game not connected again

## Issues Resolved (December 2, 2025)

### Issue: Connection Timeout During Launch

**Problem**: The `s1_launch_game` tool was timing out even though the game launched successfully and the TCP server started. Client would connect immediately but handshake would timeout.

**Root Cause**: The TCP server accepts connections immediately on game startup, but cannot process commands until the Unity Update loop starts and the Menu scene loads (~16-20 seconds after launch). The client was connecting too early, causing handshake requests to sit in the server's command queue waiting for the Unity thread.

**Solution**: Added 20-second initial delay in `_poll_connection()` before first connection attempt. This gives the game time to:
1. Start the TCP server
2. Load the Menu scene
3. Begin processing Unity Update loop
4. Process queued commands

**Code Changes**:
- `game_lifecycle_tools.py:_poll_connection()`: Added 20s delay before first connection
- `game_lifecycle_tools.py:handle_s1_launch_game()`: Updated wait time messaging to account for loading delay
- `game_lifecycle_tools.py:handle_s1_close_game()`: Improved process termination verification with retry logic (up to 3s with 300ms checks)

**Test Results**: ✅ Connection now succeeds on first attempt after ~25s (20s delay + 5s connection/handshake)

## Benefits

1. **Better UX**: MCP server can start without game running
2. **Clear Errors**: Helpful messages when trying to use tools without connection
3. **Proper Lifecycle**: Game launch → connect → use tools → close is now explicit
4. **State Tracking**: Always know if connected or not
5. **Debugging**: Added logs to help diagnose handshake/connection issues

## Files Modified

- `S1MCPClient/src/main.py`: Connection state management, guards, optional startup
- `S1MCPClient/src/tools/game_lifecycle_tools.py`: Enhanced launch/close with state updates

