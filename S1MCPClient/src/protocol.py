"""JSON-RPC protocol handling for Named Pipe communication."""

import json
import struct
from typing import Any, Dict, Optional

from .models.request import Request
from .models.response import Response, ErrorResponse


class ProtocolError(Exception):
    """Protocol-related error."""
    pass


def serialize_request(request: Request) -> bytes:
    """
    Serialize a request to the length-prefixed format.
    
    Format: [4 bytes: length] [JSON string]
    
    Args:
        request: Request object to serialize
    
    Returns:
        Serialized message bytes
    """
    import logging
    logger = logging.getLogger("S1MCPClient")
    
    # Convert to dict with camelCase keys
    json_dict = {
        "id": request.id,
        "method": request.method,
        "params": request.params
    }
    logger.debug(f"Serializing request: {json_dict}")
    
    # Serialize to JSON
    json_str = json.dumps(json_dict, ensure_ascii=False)
    json_bytes = json_str.encode('utf-8')
    logger.debug(f"JSON string length: {len(json_str)} chars, bytes: {len(json_bytes)}")
    logger.debug(f"JSON content: {json_str[:200]}..." if len(json_str) > 200 else f"JSON content: {json_str}")
    
    # Prepend 4-byte length (little-endian int32)
    length_bytes = struct.pack('<I', len(json_bytes))
    logger.debug(f"Length prefix: {len(json_bytes)} bytes (little-endian)")
    
    result = length_bytes + json_bytes
    logger.debug(f"Total serialized message: {len(result)} bytes")
    return result


def deserialize_response(data: bytes) -> Response:
    """
    Deserialize a response from the length-prefixed format.
    
    Args:
        data: Complete message bytes (length prefix + JSON)
    
    Returns:
        Deserialized Response object
    
    Raises:
        ProtocolError: If deserialization fails
    """
    import logging
    logger = logging.getLogger("S1MCPClient")
    
    logger.debug(f"Deserializing response: {len(data)} total bytes")
    
    if len(data) < 4:
        raise ProtocolError(f"Message too short: missing length prefix (got {len(data)} bytes)")
    
    # Extract length prefix (little-endian int32)
    message_length = struct.unpack('<I', data[:4])[0]
    logger.debug(f"Message length from prefix: {message_length} bytes")
    
    if len(data) < 4 + message_length:
        raise ProtocolError(f"Message incomplete: expected {4 + message_length} bytes, got {len(data)}")
    
    # Extract JSON payload
    json_bytes = data[4:4 + message_length]
    logger.debug(f"Extracted JSON payload: {len(json_bytes)} bytes")
    
    json_str = json_bytes.decode('utf-8')
    logger.debug(f"Decoded JSON string: {len(json_str)} chars")
    logger.debug(f"JSON content: {json_str[:200]}..." if len(json_str) > 200 else f"JSON content: {json_str}")
    
    # Parse JSON
    try:
        json_dict = json.loads(json_str)
        logger.debug(f"Parsed JSON dict: {json_dict}")
    except json.JSONDecodeError as e:
        logger.error(f"JSON decode error: {e}")
        logger.debug(f"Failed JSON string: {json_str}")
        raise ProtocolError(f"Invalid JSON: {e}") from e
    
    # Convert to Response object
    try:
        error = None
        if json_dict.get("error"):
            error_data = json_dict["error"]
            logger.debug(f"Response contains error: {error_data}")
            error = ErrorResponse(
                code=error_data.get("code", -32603),
                message=error_data.get("message", "Internal error"),
                data=error_data.get("data")
            )
        
        response = Response(
            id=json_dict.get("id", 0),
            result=json_dict.get("result"),
            error=error
        )
        logger.debug(f"Created Response object: id={response.id}, has_error={response.error is not None}, has_result={response.result is not None}")
        return response
    except (KeyError, TypeError) as e:
        logger.error(f"Error creating Response object: {e}")
        logger.debug(f"JSON dict: {json_dict}")
        raise ProtocolError(f"Invalid response structure: {e}") from e


def create_request(request_id: int, method: str, params: Optional[Dict[str, Any]] = None) -> Request:
    """
    Create a request object.
    
    Args:
        request_id: Unique request identifier
        method: Method name
        params: Optional parameters
    
    Returns:
        Request object
    """
    return Request(
        id=request_id,
        method=method,
        params=params or {}
    )


def map_error_code(mod_error_code: int) -> int:
    """
    Map mod error codes to MCP-compatible error codes.
    
    Args:
        mod_error_code: Error code from mod
    
    Returns:
        Mapped error code
    """
    # JSON-RPC standard error codes
    json_rpc_errors = {
        -32700: -32700,  # Parse error
        -32600: -32600,  # Invalid Request
        -32601: -32601,  # Method not found
        -32602: -32602,  # Invalid params
        -32603: -32603,  # Internal error
    }
    
    # Game-specific error codes (keep as-is, but ensure they're in valid range)
    if mod_error_code in json_rpc_errors:
        return mod_error_code
    
    # Game-specific errors (-32000 to -32099)
    if -32099 <= mod_error_code <= -32000:
        return mod_error_code
    
    # Default to internal error for unknown codes
    return -32603

