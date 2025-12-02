"""Configuration management for S1MCPClient."""

import json
import os
from pathlib import Path
from typing import Optional


class Config:
    """Configuration for S1MCPClient."""
    
    def __init__(
        self,
        host: str = "localhost",
        port: int = 8765,
        log_level: str = "DEBUG",
        connection_timeout: float = 5.0,
        reconnect_delay: float = 1.0,
        game_il2cpp_path: Optional[str] = None,
        game_mono_path: Optional[str] = None,
        game_executable: str = "Schedule I.exe",
        game_startup_timeout: float = 60.0,
        game_connection_poll_interval: float = 2.0
    ):
        """
        Initialize configuration.
        
        Args:
            host: TCP server hostname or IP address
            port: TCP server port number
            log_level: Logging level
            connection_timeout: Connection timeout in seconds
            reconnect_delay: Delay before reconnection attempts in seconds
            game_il2cpp_path: Path to IL2CPP game directory (optional)
            game_mono_path: Path to Mono game directory (optional)
            game_executable: Game executable name
            game_startup_timeout: Timeout for game startup in seconds
            game_connection_poll_interval: Interval for connection polling in seconds
        """
        self.host = host
        self.port = port
        self.log_level = log_level
        self.connection_timeout = connection_timeout
        self.reconnect_delay = reconnect_delay
        self.game_il2cpp_path = game_il2cpp_path
        self.game_mono_path = game_mono_path
        self.game_executable = game_executable
        self.game_startup_timeout = game_startup_timeout
        self.game_connection_poll_interval = game_connection_poll_interval
    
    @classmethod
    def from_file(cls, config_path: Optional[str] = None) -> "Config":
        """
        Load configuration from a JSON file.
        
        Args:
            config_path: Path to config file (defaults to config.json in project root)
        
        Returns:
            Config instance
        """
        if config_path is None:
            # Look for config.json in project root
            project_root = Path(__file__).parent.parent.parent
            config_path = project_root / "config.json"
        else:
            config_path = Path(config_path)
        
        if not config_path.exists():
            # Return default config if file doesn't exist
            return cls()
        
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            return cls(
                host=data.get("host", "localhost"),
                port=int(data.get("port", 8765)),
                log_level=data.get("log_level", "DEBUG"),
                connection_timeout=float(data.get("connection_timeout", 5.0)),
                reconnect_delay=float(data.get("reconnect_delay", 1.0)),
                game_il2cpp_path=data.get("game_il2cpp_path"),
                game_mono_path=data.get("game_mono_path"),
                game_executable=data.get("game_executable", "Schedule I.exe"),
                game_startup_timeout=float(data.get("game_startup_timeout", 60.0)),
                game_connection_poll_interval=float(data.get("game_connection_poll_interval", 2.0))
            )
        except (json.JSONDecodeError, KeyError, ValueError) as e:
            # Return default config on error
            import logging
            logger = logging.getLogger("S1MCPClient")
            logger.warning(f"Failed to load config from {config_path}: {e}. Using defaults.")
            return cls()
    
    def to_dict(self) -> dict:
        """Convert config to dictionary."""
        return {
            "host": self.host,
            "port": self.port,
            "log_level": self.log_level,
            "connection_timeout": self.connection_timeout,
            "reconnect_delay": self.reconnect_delay,
            "game_il2cpp_path": self.game_il2cpp_path,
            "game_mono_path": self.game_mono_path,
            "game_executable": self.game_executable,
            "game_startup_timeout": self.game_startup_timeout,
            "game_connection_poll_interval": self.game_connection_poll_interval
        }
    
    def save(self, config_path: Optional[str] = None) -> None:
        """
        Save configuration to a JSON file.
        
        Args:
            config_path: Path to config file (defaults to config.json in project root)
        """
        if config_path is None:
            project_root = Path(__file__).parent.parent.parent
            config_path = project_root / "config.json"
        else:
            config_path = Path(config_path)
        
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(self.to_dict(), f, indent=2)

