"""Item-related MCP tools."""

from typing import Any, Dict, Optional
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient
from ..utils.logger import get_logger


logger = get_logger()


def get_item_tools(tcp_client: TcpClient) -> list[Tool]:
    """
    Get all Item-related MCP tools.
    
    Args:
        tcp_client: TCP client instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_list_items",
            description="List all item definitions in the game, optionally filtered by category",
            inputSchema={
                "type": "object",
                "properties": {
                    "category": {
                        "type": "string",
                        "description": "Optional category filter"
                    }
                },
                "required": []
            }
        ),
        Tool(
            name="s1_get_item",
            description="Get detailed information about an item definition by ID",
            inputSchema={
                "type": "object",
                "properties": {
                    "item_id": {
                        "type": "string",
                        "description": "The unique identifier of the item"
                    }
                },
                "required": ["item_id"]
            }
        ),
        Tool(
            name="s1_spawn_item",
            description="Spawn an item in the world at a specific position",
            inputSchema={
                "type": "object",
                "properties": {
                    "item_id": {
                        "type": "string",
                        "description": "The unique identifier of the item"
                    },
                    "position": {
                        "type": "object",
                        "properties": {
                            "x": {"type": "number", "description": "X coordinate"},
                            "y": {"type": "number", "description": "Y coordinate"},
                            "z": {"type": "number", "description": "Z coordinate"}
                        },
                        "required": ["x", "y", "z"],
                        "description": "Spawn position coordinates"
                    },
                    "quantity": {
                        "type": "number",
                        "description": "Number of items to spawn (default: 1)",
                        "default": 1
                    }
                },
                "required": ["item_id", "position"]
            }
        )
    ]


async def handle_s1_list_items(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_list_items tool call."""
    category = arguments.get("category")
    params = {}
    if category:
        params["category"] = category
    
    try:
        response = tcp_client.call_with_retry("list_items", params if params else None)
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_list_items: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_item(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_item tool call."""
    item_id = arguments.get("item_id")
    if not item_id:
        return [TextContent(type="text", text="Error: item_id is required")]
    
    try:
        response = tcp_client.call_with_retry("get_item", {"item_id": item_id})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_item: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_spawn_item(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_spawn_item tool call."""
    item_id = arguments.get("item_id")
    position = arguments.get("position")
    quantity = arguments.get("quantity", 1)
    
    if not item_id:
        return [TextContent(type="text", text="Error: item_id is required")]
    if not position:
        return [TextContent(type="text", text="Error: position is required")]
    
    try:
        params = {
            "item_id": item_id,
            "position": position
        }
        if quantity != 1:
            params["quantity"] = quantity
        
        response = tcp_client.call_with_retry("spawn_item", params)
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_spawn_item: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_list_items": handle_s1_list_items,
    "s1_get_item": handle_s1_get_item,
    "s1_spawn_item": handle_s1_spawn_item,
}

