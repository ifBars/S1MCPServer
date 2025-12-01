"""Logging utilities for S1MCPClient."""

import logging
import sys
from typing import Optional


def setup_logger(name: str = "S1MCPClient", level: str = "INFO") -> logging.Logger:
    """
    Set up a logger with console output formatting.
    
    Args:
        name: Logger name
        level: Log level (DEBUG, INFO, WARNING, ERROR)
    
    Returns:
        Configured logger instance
    """
    logger = logging.getLogger(name)
    
    # Don't add handlers if they already exist
    if logger.handlers:
        return logger
    
    logger.setLevel(getattr(logging, level.upper(), logging.INFO))
    
    # Create console handler
    handler = logging.StreamHandler(sys.stderr)
    handler.setLevel(logger.level)
    
    # Create formatter
    formatter = logging.Formatter(
        fmt='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    handler.setFormatter(formatter)
    
    logger.addHandler(handler)
    logger.propagate = False
    
    return logger


def get_logger(name: Optional[str] = None) -> logging.Logger:
    """
    Get a logger instance.
    
    Args:
        name: Optional logger name (defaults to S1MCPClient)
    
    Returns:
        Logger instance
    """
    if name is None:
        name = "S1MCPClient"
    return logging.getLogger(name)

