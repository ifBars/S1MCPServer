"""Property-related MCP tools."""

from typing import Any, Dict
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient
from ..utils.logger import get_logger


logger = get_logger()


def get_property_tools(tcp_client: TcpClient) -> list[Tool]:
    """
    Get all Property-related MCP tools.
    
    Args:
        tcp_client: TCP client instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_list_properties",
            description="List all properties in the game",
            inputSchema={
                "type": "object",
                "properties": {},
                "required": []
            }
        ),
        Tool(
            name="s1_get_property",
            description="Get detailed information about a property by ID or name",
            inputSchema={
                "type": "object",
                "properties": {
                    "property_id": {
                        "type": "string",
                        "description": "The unique identifier of the property"
                    },
                    "property_name": {
                        "type": "string",
                        "description": "The name of the property (alternative to property_id)"
                    }
                },
                "required": []
            }
        )
    ]


async def handle_s1_list_properties(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_list_properties tool call."""
    try:
        response = tcp_client.call_with_retry("list_properties", {})
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_list_properties: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_property(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_property tool call."""
    property_id = arguments.get("property_id")
    property_name = arguments.get("property_name")
    
    if not property_id and not property_name:
        return [TextContent(type="text", text="Error: property_id or property_name is required")]
    
    try:
        params = {}
        if property_id:
            params["property_id"] = property_id
        if property_name:
            params["property_name"] = property_name
        
        response = tcp_client.call_with_retry("get_property", params)
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_property: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_list_properties": handle_s1_list_properties,
    "s1_get_property": handle_s1_get_property,
}

