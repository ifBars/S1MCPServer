"""Vehicle-related MCP tools."""

from typing import Any, Dict
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient
from ..utils.logger import get_logger


logger = get_logger()


def get_vehicle_tools(tcp_client: TcpClient) -> list[Tool]:
    """
    Get all Vehicle-related MCP tools.
    
    Args:
        tcp_client: TCP client instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_list_vehicles",
            description="List all vehicles in the game",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="s1_get_vehicle",
            description="Get detailed information about a vehicle by ID",
            inputSchema={
                "type": "object",
                "properties": {
                    "vehicle_id": {
                        "type": "string",
                        "description": "The unique identifier of the vehicle"
                    }
                },
                "required": ["vehicle_id"]
            }
        )
    ]


async def handle_s1_list_vehicles(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_list_vehicles tool call."""
    try:
        response = tcp_client.call_with_retry("list_vehicles", {})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_list_vehicles: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_vehicle(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_vehicle tool call."""
    vehicle_id = arguments.get("vehicle_id")
    if not vehicle_id:
        return [TextContent(type="text", text="Error: vehicle_id is required")]
    
    try:
        response = tcp_client.call_with_retry("get_vehicle", {"vehicle_id": vehicle_id})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_vehicle: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_list_vehicles": handle_s1_list_vehicles,
    "s1_get_vehicle": handle_s1_get_vehicle,
}

