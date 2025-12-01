"""Player-related MCP tools."""

from typing import Any, Dict
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient
from ..utils.logger import get_logger


logger = get_logger()


def get_player_tools(tcp_client: TcpClient) -> list[Tool]:
    """
    Get all Player-related MCP tools.
    
    Args:
        tcp_client: TCP client instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_get_player",
            description="Get current player information including position, health, money, and network status",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="s1_get_player_inventory",
            description="Get the player's inventory items",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="s1_teleport_player",
            description="Teleport the player to a specific position",
            inputSchema={
                "type": "object",
                "properties": {
                    "position": {
                        "type": "object",
                        "properties": {
                            "x": {"type": "number", "description": "X coordinate"},
                            "y": {"type": "number", "description": "Y coordinate"},
                            "z": {"type": "number", "description": "Z coordinate"}
                        },
                        "required": ["x", "y", "z"],
                        "description": "Target position coordinates"
                    }
                },
                "required": ["position"]
            }
        ),
        Tool(
            name="s1_add_item_to_player",
            description="Add item(s) to the player's inventory",
            inputSchema={
                "type": "object",
                "properties": {
                    "item_id": {
                        "type": "string",
                        "description": "The unique identifier of the item"
                    },
                    "quantity": {
                        "type": "number",
                        "description": "Number of items to add (default: 1)",
                        "default": 1
                    }
                },
                "required": ["item_id"]
            }
        )
    ]


async def handle_s1_get_player(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_player tool call."""
    try:
        response = tcp_client.call_with_retry("get_player", {})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_player: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_player_inventory(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_player_inventory tool call."""
    try:
        response = tcp_client.call_with_retry("get_player_inventory", {})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_player_inventory: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_teleport_player(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_teleport_player tool call."""
    position = arguments.get("position")
    
    if not position:
        return [TextContent(type="text", text="Error: position is required")]
    
    try:
        response = tcp_client.call_with_retry("teleport_player", {"position": position})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_teleport_player: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_add_item_to_player(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_add_item_to_player tool call."""
    item_id = arguments.get("item_id")
    quantity = arguments.get("quantity", 1)
    
    if not item_id:
        return [TextContent(type="text", text="Error: item_id is required")]
    
    try:
        response = tcp_client.call_with_retry("add_item_to_player", {
            "item_id": item_id,
            "quantity": quantity
        })
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_add_item_to_player: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_get_player": handle_s1_get_player,
    "s1_get_player_inventory": handle_s1_get_player_inventory,
    "s1_teleport_player": handle_s1_teleport_player,
    "s1_add_item_to_player": handle_s1_add_item_to_player,
}

