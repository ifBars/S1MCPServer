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
        ),
        Tool(
            name="s1_inspect_component",
            description="Inspect any component by type name with deep reflection. Supports partial type name matching (e.g., 'Dealer', 'NPC'). Can inspect from a specific GameObject, NPC ID, or find first match in scene.",
            inputSchema={
                "type": "object",
                "properties": {
                    "component_type": {
                        "type": "string",
                        "description": "The component type name (supports partial matching, e.g., 'Dealer', 'NPC', 'NPCPrefabIdentity')"
                    },
                    "object_name": {
                        "type": "string",
                        "description": "Optional: The GameObject name to inspect component on. If not provided, finds first match in scene."
                    },
                    "npc_id": {
                        "type": "string",
                        "description": "Optional: The NPC ID to inspect component on. Alternative to object_name."
                    },
                    "max_depth": {
                        "type": "integer",
                        "description": "Maximum depth for deep inspection (default: 3)",
                        "default": 3
                    }
                },
                "required": ["component_type"]
            }
        ),
        Tool(
            name="s1_get_member_value",
            description="Get any property or field value from an object, supporting nested access (e.g., 'Dealer.Home.BuildingName'). Works on GameObjects, NPCs (by ID), or specific components.",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "The name of the GameObject (optional if npc_id is provided)"
                    },
                    "npc_id": {
                        "type": "string",
                        "description": "Optional: The NPC ID to get member value from. Alternative to object_name."
                    },
                    "member_path": {
                        "type": "string",
                        "description": "The member path (supports nested access with dots, e.g., 'Dealer.Home.BuildingName')"
                    },
                    "component_type": {
                        "type": "string",
                        "description": "Optional: Component type name if accessing a component member (e.g., 'Dealer', 'NPC')"
                    }
                },
                "required": ["member_path"]
            }
        ),
        Tool(
            name="s1_get_component_by_type",
            description="Find a component by type name on a specific GameObject. Quick lookup to verify a component exists.",
            inputSchema={
                "type": "object",
                "properties": {
                    "object_name": {
                        "type": "string",
                        "description": "The name of the GameObject"
                    },
                    "component_type": {
                        "type": "string",
                        "description": "The component type name (supports partial matching)"
                    }
                },
                "required": ["object_name", "component_type"]
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


async def handle_s1_inspect_component(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_inspect_component tool call."""
    component_type = arguments.get("component_type")
    object_name = arguments.get("object_name")
    npc_id = arguments.get("npc_id")
    max_depth = arguments.get("max_depth", 3)
    
    if not component_type:
        return [TextContent(type="text", text="Error: component_type is required")]
    
    try:
        params = {
            "component_type": component_type,
            "max_depth": max_depth
        }
        if object_name:
            params["object_name"] = object_name
        if npc_id:
            params["npc_id"] = npc_id
        
        response = tcp_client.call_with_retry("inspect_component", params)
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_inspect_component: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_member_value(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_member_value tool call."""
    object_name = arguments.get("object_name")
    npc_id = arguments.get("npc_id")
    member_path = arguments.get("member_path")
    component_type = arguments.get("component_type")
    
    if not member_path:
        return [TextContent(type="text", text="Error: member_path is required")]
    
    if not object_name and not npc_id:
        return [TextContent(type="text", text="Error: either object_name or npc_id is required")]
    
    try:
        params = {
            "member_path": member_path
        }
        if object_name:
            params["object_name"] = object_name
        if npc_id:
            params["npc_id"] = npc_id
        if component_type:
            params["component_type"] = component_type
        
        response = tcp_client.call_with_retry("get_member_value", params)
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_member_value: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


async def handle_s1_get_component_by_type(arguments: Dict[str, Any], tcp_client: TcpClient) -> list[TextContent]:
    """Handle s1_get_component_by_type tool call."""
    object_name = arguments.get("object_name")
    component_type = arguments.get("component_type")
    
    if not object_name or not component_type:
        return [TextContent(type="text", text="Error: object_name and component_type are required")]
    
    try:
        response = tcp_client.call_with_retry("get_component_by_type", {
            "object_name": object_name,
            "component_type": component_type
        })
        
        if response.error:
            return [TextContent(
                type="text",
                text=f"Error: {response.error.message} (code: {response.error.code})"
            )]
        
        import json
        return [TextContent(type="text", text=json.dumps(response.result, indent=2))]
    except Exception as e:
        logger.error(f"Error in s1_get_component_by_type: {e}")
        return [TextContent(type="text", text=f"Error: {str(e)}")]


# Tool handler mapping
TOOL_HANDLERS = {
    "s1_inspect_object": handle_s1_inspect_object,
    "s1_inspect_component": handle_s1_inspect_component,
    "s1_get_member_value": handle_s1_get_member_value,
    "s1_get_component_by_type": handle_s1_get_component_by_type,
}

