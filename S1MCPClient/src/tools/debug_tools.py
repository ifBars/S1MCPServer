"""Debug/inspection MCP tools."""

from typing import Any, Dict
from mcp.types import Tool, TextContent

from ..tcp_client import TcpClient
from ..utils.logger import get_logger


logger = get_logger()


def get_debug_tools(tcp_client: TcpClient) -> list[Tool]:
    """
    Get all Debug MCP tools.
    
    Args:
        tcp_client: TCP client instance
    
    Returns:
        List of MCP Tool definitions
    """
    return [
        Tool(
            name="s1_inspect_object",
            description="Inspect a Unity GameObject or component using reflection. Useful for debugging and discovering game object properties.",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "The name of the GameObject to inspect"
                    },
                    "object_type": {
                        "type": "string",
                        "description": "The type of object to inspect (e.g., 'GameObject', 'Component', or a specific component type name)",
                        "default": "GameObject"
                    }
                },
                "required": ["object_name"]
            }
        )
    ]


async def handle_s1_inspect_object(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_inspect_object tool call."""
    object_name = arguments.get("object_name")
    object_type = arguments.get("object_type", "GameObject")
    
    if not object_name:
        return [TextContent(type="text", text="Error: object_name is required")]
    
    try:
        response = tcp_client.call_with_retry("inspect_object", {
            "object_name": object_name,
            "object_type": object_type
        })
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_inspect_object: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_inspect_object": handle_s1_inspect_object,
}

