"""Main entry point for S1MCPClient MCP server."""

import asyncio
import sys
from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import Tool

from .tcp_client import TcpClient, TcpConnectionError
from .utils.config import Config
from .utils.logger import setup_logger, get_logger

# Import all tool modules
from .tools import npc_tools
from .tools import player_tools
from .tools import item_tools
from .tools import property_tools
from .tools import vehicle_tools
from .tools import game_state_tools
from .tools import debug_tools


# Global TCP client instance
tcp_client: TcpClient | None = None
logger = get_logger()


def create_server(config: Config, tcp_client: TcpClient) -> Server:
    """
    Create and configure the MCP server.
    
    Args:
        config: Configuration instance
        pipe_client: Named pipe client instance
    
    Returns:
        Configured MCP server
    """
    server = Server("s1mcpclient")
    
    # Collect all tools
    all_tools: list[Tool] = []
    all_tool_handlers: dict[str, callable] = {}
    
    # NPC tools
    all_tools.extend(npc_tools.get_npc_tools(tcp_client))
    all_tool_handlers.update(npc_tools.TOOL_HANDLERS)
    
    # Player tools
    all_tools.extend(player_tools.get_player_tools(tcp_client))
    all_tool_handlers.update(player_tools.TOOL_HANDLERS)
    
    # Item tools
    all_tools.extend(item_tools.get_item_tools(tcp_client))
    all_tool_handlers.update(item_tools.TOOL_HANDLERS)
    
    # Property tools
    all_tools.extend(property_tools.get_property_tools(tcp_client))
    all_tool_handlers.update(property_tools.TOOL_HANDLERS)
    
    # Vehicle tools
    all_tools.extend(vehicle_tools.get_vehicle_tools(tcp_client))
    all_tool_handlers.update(vehicle_tools.TOOL_HANDLERS)
    
    # Game state tools
    all_tools.extend(game_state_tools.get_game_state_tools(tcp_client))
    all_tool_handlers.update(game_state_tools.TOOL_HANDLERS)
    
    # Debug tools
    all_tools.extend(debug_tools.get_debug_tools(tcp_client))
    all_tool_handlers.update(debug_tools.TOOL_HANDLERS)
    
    # Register list_tools handler
    @server.list_tools()
    async def handle_list_tools() -> list[Tool]:
        """List all available tools."""
        return all_tools
    
    # Register call_tool handler
    @server.call_tool()
    async def handle_call_tool(name: str, arguments: dict) -> list:
        """
        Handle tool calls.
        
        Args:
            name: Tool name
            arguments: Tool arguments
        
        Returns:
            Tool result
        """
        logger.debug(f"Tool call received: {name} with arguments: {arguments}")
        
        if name not in all_tool_handlers:
            logger.error(f"Unknown tool: {name}")
            logger.debug(f"Available tools: {list(all_tool_handlers.keys())}")
            raise ValueError(f"Unknown tool: {name}")
        
        handler = all_tool_handlers[name]
        logger.debug(f"Found handler for {name}, invoking...")
        
        try:
            result = await handler(arguments, tcp_client)
            logger.debug(f"Tool {name} completed successfully, result type: {type(result)}")
            return result
        except Exception as e:
            logger.error(f"Error in tool handler {name}: {e}", exc_info=True)
            raise
    
    return server


async def main():
    """Main entry point."""
    global tcp_client
    
    # Prevent multiple instances - check if we're already running
    import atexit
    import os
    import tempfile
    
    pid_file = os.path.join(tempfile.gettempdir(), "s1mcpclient.pid")
    if os.path.exists(pid_file):
        try:
            with open(pid_file, 'r') as f:
                old_pid = int(f.read().strip())
            # Check if the process is still running
            try:
                os.kill(old_pid, 0)  # Signal 0 just checks if process exists
                logger.warning(f"Another instance appears to be running (PID: {old_pid}). Continuing anyway...")
            except (OSError, ProcessLookupError):
                # Process doesn't exist, remove stale PID file
                os.remove(pid_file)
        except (ValueError, IOError):
            # Invalid PID file, remove it
            try:
                os.remove(pid_file)
            except:
                pass
    
    # Write our PID
    try:
        with open(pid_file, 'w') as f:
            f.write(str(os.getpid()))
        
        def cleanup_pid():
            try:
                if os.path.exists(pid_file):
                    os.remove(pid_file)
            except:
                pass
        
        atexit.register(cleanup_pid)
    except Exception as e:
        logger.debug(f"Could not create PID file: {e}")
    
    # Load configuration
    config = Config.from_file()
    
    # Setup logger
    setup_logger(level=config.log_level)
    logger.info("Starting S1MCPClient MCP server...")
    
    # Initialize TCP client
    try:
        tcp_client = TcpClient(
            host=config.host,
            port=config.port,
            timeout=config.connection_timeout,
            reconnect_delay=config.reconnect_delay
        )
        
        # Try to connect (will retry on first use if it fails)
        try:
            logger.debug("Attempting initial connection to mod...")
            tcp_client.connect()
            logger.info("Connected to mod successfully")
            
            # Perform handshake to verify connection and get available methods
            try:
                logger.debug("Performing handshake with mod...")
                handshake_response = tcp_client.call("handshake", {})
                
                if handshake_response.error:
                    logger.warning(f"Handshake failed: {handshake_response.error.message}")
                else:
                    handshake_data = handshake_response.result
                    if isinstance(handshake_data, dict):
                        available_methods = handshake_data.get("available_methods", [])
                        total_methods = handshake_data.get("total_methods", 0)
                        server_name = handshake_data.get("server_name", "Unknown")
                        version = handshake_data.get("version", "Unknown")
                        
                        logger.info(f"Handshake successful: {server_name} v{version}")
                        logger.info(f"Available methods: {total_methods}")
                        logger.debug(f"Methods: {', '.join(available_methods)}")
                        
                        # Log method categories if available
                        if "method_categories" in handshake_data:
                            categories = handshake_data["method_categories"]
                            for category, methods in categories.items():
                                if methods:
                                    logger.debug(f"  {category}: {len(methods)} methods")
                        
                        # Log integrations
                        if "integrations" in handshake_data:
                            integrations = handshake_data["integrations"]
                            logger.debug(f"Integrations: {integrations}")
                    else:
                        logger.warning("Handshake response format unexpected")
            except Exception as e:
                logger.warning(f"Handshake failed: {e}. Connection may still work.")
                logger.debug(f"Handshake error details: {e}", exc_info=True)
                
        except TcpConnectionError as e:
            logger.warning(f"Initial connection failed: {e}. Will retry on first tool call.")
            logger.debug(f"Connection error details: {e}")
        
    except Exception as e:
        logger.error(f"Failed to initialize TCP client: {e}")
        sys.exit(1)
    
    # Create server
    try:
        server = create_server(config, tcp_client)
        logger.info("MCP server created, registering tools...")
    except Exception as e:
        logger.error(f"Failed to create server: {e}")
        sys.exit(1)
    
    # Run server with stdio transport
    try:
        logger.info("Starting MCP server with stdio transport...")
        async with stdio_server() as (read_stream, write_stream):
            await server.run(
                read_stream,
                write_stream
            )
    except KeyboardInterrupt:
        logger.info("Received interrupt signal, shutting down...")
    except Exception as e:
        logger.error(f"Server error: {e}")
        sys.exit(1)
    finally:
        # Cleanup
        if tcp_client:
            try:
                tcp_client.disconnect()
            except Exception as e:
                logger.warning(f"Error disconnecting TCP client: {e}")
        logger.info("MCP server stopped")


def entry_point():
    """Entry point for the application."""
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        logger.info("Interrupted by user")
        sys.exit(0)
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    entry_point()

