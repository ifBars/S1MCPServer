"""Response model for JSON-RPC communication."""

from typing import Any, Dict, Optional
from pydantic import BaseModel, Field


class ErrorResponse(BaseModel):
    """Error response structure."""
    
    code: int = Field(..., description="Error code")
    message: str = Field(..., description="Human-readable error message")
    data: Optional[Dict[str, Any]] = Field(None, description="Additional error data")


class Response(BaseModel):
    """JSON-RPC response model matching the mod's Response structure."""
    
    id: int = Field(..., description="Request ID that this response corresponds to")
    result: Optional[Any] = Field(None, description="Result object (null if error occurred)")
    error: Optional[ErrorResponse] = Field(None, description="Error object (null if successful)")
    
    class Config:
        """Pydantic configuration."""
        json_schema_extra = {
            "example": {
                "id": 1,
                "result": {
                    "npc_id": "kyle_cooley",
                    "name": "Kyle Cooley",
                    "position": {"x": 10.5, "y": 1.0, "z": 20.3}
                },
                "error": None
            }
        }

