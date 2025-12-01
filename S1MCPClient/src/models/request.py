"""Request model for JSON-RPC communication."""

from typing import Any, Dict, Optional
from pydantic import BaseModel, Field


class Request(BaseModel):
    """JSON-RPC request model matching the mod's Request structure."""
    
    id: int = Field(..., description="Unique request identifier")
    method: str = Field(..., description="Method name to invoke")
    params: Optional[Dict[str, Any]] = Field(None, description="Method parameters as a JSON object")
    
    class Config:
        """Pydantic configuration."""
        json_schema_extra = {
            "example": {
                "id": 1,
                "method": "get_npc",
                "params": {
                    "npc_id": "kyle_cooley"
                }
            }
        }

