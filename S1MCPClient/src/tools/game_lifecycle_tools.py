"""Game lifecycle management MCP tools."""

import asyncio
import os
import subprocess
import time
from pathlib import Path
from typing import Any, Dict, Optional
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient, TcpConnectionError
from ..utils.logger import get_logger
from ..utils.config import Config


logger = get_logger()


def get_game_lifecycle_tools(tcp_client: TcpClient, config: Config) -> list[Tool]:
    """
    Get all Game Lifecycle MCP tools.
    
    Args:
        tcp_client: TCP client instance
        config: Configuration instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_launch_game",
            description=(
                "Launch the Schedule I game with specified version (IL2CPP or Mono) and optional debugging. "
                "Automatically waits for the game to start and establishes connection to the mod server. "
                "Use this to start the game before testing mods."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "version": {
                        "type": "string",
                        "enum": ["il2cpp", "mono"],
                        "description": "Game version to launch: 'il2cpp' or 'mono'"
                    },
                    "enable_debugger": {
                        "type": "boolean",
                        "description": "Enable MelonLoader debugger (adds --melonloader.launchdebugger --melonloader.debug flags)",
                        "default": False
                    },
                    "wait_for_connection": {
                        "type": "boolean",
                        "description": "Wait for game to start and automatically connect to the mod server",
                        "default": True
                    }
                },
                "required": ["version"]
            }
        ),
        Tool(
            name="s1_close_game",
            description=(
                "Forcefully close the Schedule I game. "
                "Use this to terminate the game after testing or before relaunching with different settings."
            ),
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="s1_get_game_process_info",
            description=(
                "Check if the Schedule I game is currently running and get process information. "
                "Returns running status, process ID(s), and resource usage."
            ),
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        )
    ]


# Helper functions

def _detect_game_version(game_path: str) -> Optional[str]:
    """
    Detect if a game installation is IL2CPP or Mono.
    
    Args:
        game_path: Path to game directory
    
    Returns:
        'il2cpp' if IL2CPP assemblies folder exists, 'mono' otherwise, None if invalid path
    """
    try:
        path = Path(game_path)
        if not path.exists():
            logger.warning(f"Game path does not exist: {game_path}")
            return None
        
        # Check for IL2CPP marker: MelonLoader/Il2CppAssemblies folder
        il2cpp_marker = path / "MelonLoader" / "Il2CppAssemblies"
        if il2cpp_marker.exists() and il2cpp_marker.is_dir():
            logger.debug(f"Detected IL2CPP version at {game_path}")
            return "il2cpp"
        else:
            logger.debug(f"Detected Mono version at {game_path}")
            return "mono"
    except Exception as e:
        logger.error(f"Error detecting game version at {game_path}: {e}")
        return None


def _is_game_running() -> bool:
    """
    Check if the Schedule I game process is currently running.
    
    Returns:
        True if game is running, False otherwise
    """
    try:
        # Use tasklist to check for the process
        result = subprocess.run(
            ['tasklist', '/FI', 'IMAGENAME eq Schedule I.exe'],
            capture_output=True,
            text=True,
            timeout=5
        )
        # If the process is running, its name will appear in the output
        is_running = 'Schedule I.exe' in result.stdout
        logger.debug(f"Game running check: {is_running}")
        return is_running
    except Exception as e:
        logger.error(f"Error checking if game is running: {e}")
        return False


def _get_game_process_info() -> Dict[str, Any]:
    """
    Get detailed information about running Schedule I game processes.
    
    Returns:
        Dictionary with process information
    """
    try:
        # Use PowerShell to get detailed process info
        ps_command = (
            'Get-Process -Name "Schedule I" -ErrorAction SilentlyContinue | '
            'Select-Object Id, CPU, WorkingSet, StartTime | '
            'ConvertTo-Json'
        )
        
        result = subprocess.run(
            ['powershell', '-Command', ps_command],
            capture_output=True,
            text=True,
            timeout=10
        )
        
        if result.returncode == 0 and result.stdout.strip():
            import json
            processes = json.loads(result.stdout)
            
            # Handle single process (not array) or array of processes
            if not isinstance(processes, list):
                processes = [processes]
            
            return {
                "running": True,
                "process_count": len(processes),
                "processes": [
                    {
                        "pid": p.get("Id"),
                        "cpu_time": p.get("CPU"),
                        "memory_mb": round(p.get("WorkingSet", 0) / 1024 / 1024, 2),
                        "start_time": p.get("StartTime")
                    }
                    for p in processes
                ]
            }
        else:
            return {
                "running": False,
                "process_count": 0,
                "processes": []
            }
    except Exception as e:
        logger.error(f"Error getting game process info: {e}")
        return {
            "running": False,
            "error": str(e)
        }


def _wait_for_game_launch(timeout: float = 10.0) -> bool:
    """
    Wait for the game process to appear.
    
    Args:
        timeout: Maximum time to wait in seconds
    
    Returns:
        True if game launched, False if timeout
    """
    start_time = time.time()
    while time.time() - start_time < timeout:
        if _is_game_running():
            logger.info("Game process detected")
            return True
        time.sleep(0.5)
    
    logger.warning(f"Game process not detected after {timeout} seconds")
    return False


async def _poll_connection(tcp_client: TcpClient, timeout: float, interval: float) -> Dict[str, Any]:
    """
    Poll for successful connection to the game server.
    
    Args:
        tcp_client: TCP client instance
        timeout: Maximum time to wait in seconds
        interval: Time between connection attempts in seconds
    
    Returns:
        Dictionary with connection status and details
    """
    start_time = time.time()
    attempt = 0
    
    # Ensure we start with a clean slate - disconnect any existing connection
    logger.debug("Disconnecting any existing connection before polling...")
    try:
        tcp_client.disconnect()
    except Exception as e:
        logger.debug(f"Error disconnecting (ignoring): {e}")
    
    # Give a moment for cleanup
    await asyncio.sleep(0.5)
    
    # IMPORTANT: Wait for game to fully load before first connection attempt
    # The TCP server starts immediately but can't process commands until the
    # Menu scene is loaded (~16-20 seconds after game launch)
    initial_delay = 20.0  # Wait 20 seconds before first attempt
    logger.info(f"Waiting {initial_delay}s for game to fully load before connecting...")
    await asyncio.sleep(initial_delay)
    logger.debug("Initial delay complete, starting connection attempts...")
    
    while time.time() - start_time < timeout:
        attempt += 1
        try:
            logger.debug(f"Connection attempt {attempt}...")
            
            # Force disconnect before each attempt to ensure clean state
            if tcp_client.is_connected():
                logger.debug("Client thinks it's connected, forcing disconnect...")
                tcp_client.disconnect()
                await asyncio.sleep(0.2)
            
            # Try to connect
            logger.debug(f"Attempting fresh connection to {tcp_client.host}:{tcp_client.port}...")
            tcp_client.connect()
            logger.debug("Connection established, performing handshake...")
            
            # Perform handshake to verify connection
            response = tcp_client.call("handshake", {})
            
            if response.error:
                logger.debug(f"Handshake failed: {response.error.message}")
                tcp_client.disconnect()
            else:
                logger.info(f"Successfully connected to game on attempt {attempt}")
                
                # Log handshake response details for debugging
                if isinstance(response.result, dict):
                    logger.debug(f"Handshake result keys: {response.result.keys()}")
                    if "instructions" in response.result:
                        logger.info(f"Received instructions: {len(response.result['instructions'])} characters")
                    else:
                        logger.warning("No 'instructions' key in handshake response!")
                else:
                    logger.warning(f"Handshake result is not a dict: {type(response.result)}")
                
                return {
                    "connected": True,
                    "attempts": attempt,
                    "elapsed_time": round(time.time() - start_time, 2),
                    "server_info": response.result
                }
        except TcpConnectionError as e:
            logger.debug(f"Connection attempt {attempt} failed: {e}")
            try:
                tcp_client.disconnect()
            except Exception:
                pass
        except Exception as e:
            logger.debug(f"Unexpected error on connection attempt {attempt}: {e}")
            try:
                tcp_client.disconnect()
            except Exception:
                pass
        
        # Wait before next attempt
        if time.time() - start_time < timeout:
            logger.debug(f"Waiting {interval}s before next attempt...")
            await asyncio.sleep(interval)
    
    logger.warning(f"Connection polling timed out after {attempt} attempts")
    return {
        "connected": False,
        "attempts": attempt,
        "elapsed_time": round(time.time() - start_time, 2),
        "error": f"Connection timeout after {timeout} seconds ({attempt} attempts)"
    }


# Tool handlers

async def handle_s1_launch_game(arguments: Dict[str, Any], tcp_client: TcpClient, config: Config) -> list[TextContent]:
    """Handle s1_launch_game tool call."""
    try:
        version = arguments.get("version", "").lower()
        enable_debugger = arguments.get("enable_debugger", False)
        wait_for_connection = arguments.get("wait_for_connection", True)
        
        # Validate version
        if version not in ["il2cpp", "mono"]:
            return [TextContent(
                type="text",
                text=f"Error: Invalid version '{version}'. Must be 'il2cpp' or 'mono'."
            )]
        
        # Get game path from config
        game_path = config.game_il2cpp_path if version == "il2cpp" else config.game_mono_path
        
        if not game_path:
            return [TextContent(
                type="text",
                text=(
                    f"Error: No game path configured for {version.upper()} version.\n"
                    f"Please set 'game_{version}_path' in your config.json file.\n"
                    f"Example: game_config.json.example"
                )
            )]
        
        # Validate path exists
        if not Path(game_path).exists():
            return [TextContent(
                type="text",
                text=f"Error: Game path does not exist: {game_path}"
            )]
        
        # Detect and verify game version
        detected_version = _detect_game_version(game_path)
        if detected_version and detected_version != version:
            logger.warning(
                f"Warning: Requested {version.upper()} but detected {detected_version.upper()} at {game_path}"
            )
        
        # Check if game is already running
        if _is_game_running():
            return [TextContent(
                type="text",
                text=(
                    "Error: Game is already running.\n"
                    "Please close the game first using s1_close_game, or kill it manually."
                )
            )]
        
        # Construct game executable path
        game_exe = Path(game_path) / config.game_executable
        if not game_exe.exists():
            return [TextContent(
                type="text",
                text=f"Error: Game executable not found: {game_exe}"
            )]
        
        # Build launch command
        args_list = []
        if enable_debugger:
            args_list = ["--melonloader.launchdebugger", "--melonloader.debug"]
        
        # Build PowerShell command
        if args_list:
            args_str = "'" + "','".join(args_list) + "'"
            ps_command = f'Start-Process -FilePath "{game_exe}" -ArgumentList {args_str}'
        else:
            ps_command = f'Start-Process -FilePath "{game_exe}"'
        
        logger.info(f"Launching {version.upper()} game: {game_exe}")
        logger.debug(f"PowerShell command: {ps_command}")
        
        # Launch the game
        try:
            result = subprocess.run(
                ['powershell', '-Command', ps_command],
                capture_output=True,
                text=True,
                timeout=10
            )
            
            if result.returncode != 0:
                return [TextContent(
                    type="text",
                    text=f"Error launching game:\n{result.stderr}"
                )]
        except subprocess.TimeoutExpired:
            return [TextContent(
                type="text",
                text="Error: Game launch command timed out"
            )]
        except Exception as e:
            return [TextContent(
                type="text",
                text=f"Error launching game: {str(e)}"
            )]
        
        # Wait for game process to appear
        logger.info("Waiting for game process to start...")
        if not _wait_for_game_launch(timeout=10.0):
            return [TextContent(
                type="text",
                text=(
                    "Warning: Game process not detected after 10 seconds.\n"
                    "The game may be starting slowly or failed to launch."
                )
            )]
        
        response_text = f"✓ Game launched successfully ({version.upper()})\n"
        response_text += f"  Path: {game_path}\n"
        if enable_debugger:
            response_text += "  Debugger: Enabled\n"
        
        # Wait for connection if requested
        if wait_for_connection:
            # Add extra time for initial game loading delay (20s) plus connection attempts
            total_wait_time = config.game_startup_timeout + 20.0
            logger.info(f"Waiting up to {total_wait_time}s for game to load and connect...")
            response_text += f"\nWaiting for game to load and connect (up to {total_wait_time}s)...\n"
            response_text += "  - Game needs ~20s to load Menu scene before accepting commands\n"
            
            connection_result = await _poll_connection(
                tcp_client,
                timeout=config.game_startup_timeout,
                interval=config.game_connection_poll_interval
            )
            
            if connection_result["connected"]:
                response_text += f"✓ Connected to game server after {connection_result['elapsed_time']}s "
                response_text += f"({connection_result['attempts']} attempts)\n"
                
                # Update global connection state in main module
                try:
                    import sys
                    main_module = sys.modules.get('__main__') or sys.modules.get('s1mcpclient.src.main')
                    if main_module and hasattr(main_module, 'is_connected'):
                        main_module.is_connected = True
                        logger.info("Updated global connection state to connected")
                    
                    # Also update server_instructions if available
                    if "server_info" in connection_result and connection_result["server_info"]:
                        server_info = connection_result["server_info"]
                        if isinstance(server_info, dict):
                            response_text += f"  Server: {server_info.get('server_name', 'Unknown')} "
                            response_text += f"v{server_info.get('version', 'Unknown')}\n"
                            
                            # Store instructions if available
                            if "instructions" in server_info and hasattr(main_module, 'server_instructions'):
                                main_module.server_instructions = server_info["instructions"]
                                logger.info(f"Received and stored server instructions ({len(server_info['instructions'])} chars)")
                except Exception as e:
                    logger.warning(f"Could not update global connection state: {e}")
            else:
                response_text += f"✗ Failed to connect to game server\n"
                response_text += f"  Attempts: {connection_result['attempts']}\n"
                response_text += f"  Error: {connection_result.get('error', 'Unknown error')}\n"
                response_text += "\nThe game may still be loading. You can:\n"
                response_text += "  - Wait and try connecting manually\n"
                response_text += "  - Check MelonLoader logs for errors\n"
                response_text += "  - Verify the mod is installed correctly\n"
        
        import json
        return [TextContent(type="text", text=response_text)]
        
    except Exception as e:
        logger.error(f"Error in s1_launch_game: {e}", exc_info=True)
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_close_game(arguments: Dict[str, Any], tcp_client: TcpClient, config: Config) -> list[TextContent]:
    """Handle s1_close_game tool call."""
    try:
        # Check if game is running
        if not _is_game_running():
            return [TextContent(
                type="text",
                text="Game is not currently running."
            )]
        
        logger.info("Closing game...")
        
        # Disconnect TCP client first
        try:
            tcp_client.disconnect()
        except Exception as e:
            logger.debug(f"Error disconnecting client (ignoring): {e}")
        
        # Force kill the game process
        try:
            result = subprocess.run(
                ['taskkill', '/F', '/IM', config.game_executable],
                capture_output=True,
                text=True,
                timeout=5
            )
            
            # Wait for process termination with retries
            max_wait_time = 3.0  # Wait up to 3 seconds
            check_interval = 0.3  # Check every 300ms
            elapsed = 0.0
            
            while elapsed < max_wait_time:
                time.sleep(check_interval)
                elapsed += check_interval
                if not _is_game_running():
                    break
            
            # Final verification
            if not _is_game_running():
                # Update global connection state
                try:
                    import sys
                    main_module = sys.modules.get('__main__') or sys.modules.get('s1mcpclient.src.main')
                    if main_module and hasattr(main_module, 'is_connected'):
                        main_module.is_connected = False
                        logger.info("Updated global connection state to disconnected")
                except Exception as e:
                    logger.warning(f"Could not update global connection state: {e}")
                
                return [TextContent(
                    type="text",
                    text="✓ Game closed successfully"
                )]
            else:
                return [TextContent(
                    type="text",
                    text="Warning: Game process may still be running"
                )]
        except subprocess.TimeoutExpired:
            return [TextContent(
                type="text",
                text="Error: Game close command timed out"
            )]
        except Exception as e:
            return [TextContent(
                type="text",
                text=f"Error closing game: {str(e)}"
            )]
        
    except Exception as e:
        logger.error(f"Error in s1_close_game: {e}", exc_info=True)
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_game_process_info(arguments: Dict[str, Any], tcp_client: TcpClient, config: Config) -> list[TextContent]:
    """Handle s1_get_game_process_info tool call."""
    try:
        process_info = _get_game_process_info()
        
        import json
        return [TextContent(type="text", text=json.dumps(process_info, indent=2))]
        
    except Exception as e:
        logger.error(f"Error in s1_get_game_process_info: {e}", exc_info=True)
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_launch_game": handle_s1_launch_game,
    "s1_close_game": handle_s1_close_game,
    "s1_get_game_process_info": handle_s1_get_game_process_info,
}

