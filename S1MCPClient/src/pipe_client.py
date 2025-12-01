"""Windows Named Pipe client for communicating with the mod."""

import struct
import threading
import time
from typing import Any, Dict, Optional, Callable
import win32pipe
import win32file
import pywintypes

from .models.request import Request
from .models.response import Response
from .models.acknowledgment import Acknowledgment
from .protocol import serialize_request, deserialize_response, ProtocolError, create_request
from .utils.logger import get_logger


class PipeConnectionError(Exception):
    """Named pipe connection error."""
    pass


class NamedPipeClient:
    """Windows Named Pipe client for mod communication."""
    
    def __init__(self, pipe_name: str = "S1MCPServer", timeout: float = 5.0, reconnect_delay: float = 1.0):
        """
        Initialize the named pipe client.
        
        Args:
            pipe_name: Name of the named pipe (without \\.\pipe\ prefix)
            timeout: Connection timeout in seconds
            reconnect_delay: Delay before reconnection attempts in seconds
        """
        self.pipe_name = pipe_name
        self.pipe_path = f"\\\\.\\pipe\\{pipe_name}"
        self.timeout = timeout
        self.reconnect_delay = reconnect_delay
        self.logger = get_logger()
        
        self._handle: Optional[int] = None
        self._connected = False
        self._lock = threading.Lock()
        self._request_id_counter = 0
        self._request_id_lock = threading.Lock()
    
    def connect(self) -> None:
        """
        Connect to the named pipe server.
        
        Raises:
            PipeConnectionError: If connection fails
        """
        with self._lock:
            if self._connected:
                self.logger.debug("Already connected to named pipe")
                return
            
            try:
                self.logger.debug(f"Attempting to connect to named pipe: {self.pipe_path}")
                self.logger.debug(f"Connection timeout: {self.timeout}s")
                
                # Open the named pipe
                self.logger.debug("Calling win32file.CreateFile...")
                self._handle = win32file.CreateFile(
                    self.pipe_path,
                    win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                    0,
                    None,
                    win32file.OPEN_EXISTING,
                    0,
                    None
                )
                
                self._connected = True
                self.logger.info(f"Connected to named pipe successfully (handle: {self._handle})")
                self.logger.debug(f"Pipe path: {self.pipe_path}")
                self.logger.debug(f"Connection state: _connected={self._connected}, handle_valid={self._handle is not None}")
                
            except pywintypes.error as e:
                self._handle = None
                self._connected = False
                error_msg = f"Failed to connect to named pipe: {e}"
                self.logger.error(error_msg)
                self.logger.debug(f"Error code: {e.winerror}, Error message: {e.strerror}")
                raise PipeConnectionError(error_msg) from e
    
    def disconnect(self) -> None:
        """Disconnect from the named pipe server."""
        with self._lock:
            if self._handle is not None:
                try:
                    win32file.CloseHandle(self._handle)
                except Exception as e:
                    self.logger.warning(f"Error closing pipe handle: {e}")
                finally:
                    self._handle = None
                    self._connected = False
                    self.logger.info("Disconnected from named pipe")
    
    def is_connected(self) -> bool:
        """Check if connected to the pipe."""
        with self._lock:
            return self._connected and self._handle is not None
    
    def _ensure_connected(self) -> None:
        """Ensure we're connected, reconnect if necessary."""
        if not self.is_connected():
            self.logger.debug("_ensure_connected: Not connected, attempting to connect...")
            self.connect()
        else:
            self.logger.debug("_ensure_connected: Already connected")
    
    def _get_next_request_id(self) -> int:
        """Get the next request ID (thread-safe)."""
        with self._request_id_lock:
            self._request_id_counter += 1
            return self._request_id_counter
    
    def _read_message(self) -> bytes:
        """
        Read a complete message from the pipe.
        
        Returns:
            Complete message bytes (length prefix + JSON)
        
        Raises:
            PipeConnectionError: If read fails
        """
        try:
            self.logger.debug("Reading message from pipe...")
            
            # Read 4-byte length prefix
            self.logger.debug("Reading 4-byte length prefix...")
            length_bytes, _ = win32file.ReadFile(self._handle, 4)
            self.logger.debug(f"Read {len(length_bytes)} bytes for length prefix")
            
            if len(length_bytes) != 4:
                raise PipeConnectionError(f"Failed to read message length: got {len(length_bytes)} bytes instead of 4")
            
            message_length = struct.unpack('<I', length_bytes)[0]
            self.logger.debug(f"Message length: {message_length} bytes")
            
            if message_length < 0 or message_length > 10 * 1024 * 1024:  # Max 10MB
                raise PipeConnectionError(f"Invalid message length: {message_length}")
            
            # Read JSON message
            self.logger.debug(f"Reading {message_length} bytes of JSON message...")
            message_bytes, _ = win32file.ReadFile(self._handle, message_length)
            self.logger.debug(f"Read {len(message_bytes)} bytes of message data")
            
            if len(message_bytes) != message_length:
                raise PipeConnectionError(f"Incomplete message: expected {message_length} bytes, got {len(message_bytes)}")
            
            # Return length prefix + message
            total_bytes = length_bytes + message_bytes
            self.logger.debug(f"Complete message read: {len(total_bytes)} total bytes")
            return total_bytes
            
        except pywintypes.error as e:
            self.logger.error(f"Error reading from pipe: {e}")
            self.logger.debug(f"Error code: {e.winerror}, Error message: {e.strerror}")
            raise PipeConnectionError(f"Error reading from pipe: {e}") from e
    
    def _serialize_acknowledgment(self, ack: Acknowledgment) -> bytes:
        """
        Serialize an acknowledgment to the length-prefixed format.
        
        Args:
            ack: Acknowledgment object to serialize
        
        Returns:
            Serialized message bytes
        """
        import json
        import struct
        import logging
        logger = logging.getLogger("S1MCPClient")
        
        # Convert to dict
        json_dict = {
            "id": ack.id,
            "status": ack.status
        }
        logger.debug(f"Serializing acknowledgment: {json_dict}")
        
        # Serialize to JSON
        json_str = json.dumps(json_dict, ensure_ascii=False)
        json_bytes = json_str.encode('utf-8')
        logger.debug(f"Acknowledgment JSON length: {len(json_str)} chars, bytes: {len(json_bytes)}")
        
        # Prepend 4-byte length (little-endian int32)
        length_bytes = struct.pack('<I', len(json_bytes))
        
        result = length_bytes + json_bytes
        logger.debug(f"Total serialized acknowledgment: {len(result)} bytes")
        return result
    
    def _write_message(self, data: bytes) -> None:
        """
        Write a complete message to the pipe.
        
        Args:
            data: Message bytes to write
        
        Raises:
            PipeConnectionError: If write fails
        """
        try:
            self.logger.debug(f"Writing {len(data)} bytes to pipe...")
            
            # Write in chunks if needed (Windows has limits)
            chunk_size = 64 * 1024  # 64KB chunks
            offset = 0
            chunk_num = 0
            
            while offset < len(data):
                chunk = data[offset:offset + chunk_size]
                self.logger.debug(f"Writing chunk {chunk_num + 1}: {len(chunk)} bytes (offset: {offset})")
                win32file.WriteFile(self._handle, chunk)
                offset += len(chunk)
                chunk_num += 1
            
            self.logger.debug(f"Successfully wrote {len(data)} bytes in {chunk_num} chunk(s)")
            
        except pywintypes.error as e:
            self.logger.error(f"Error writing to pipe: {e}")
            self.logger.debug(f"Error code: {e.winerror}, Error message: {e.strerror}")
            raise PipeConnectionError(f"Error writing to pipe: {e}") from e
    
    def call(self, method: str, params: Optional[Dict[str, Any]] = None) -> Response:
        """
        Call a method on the mod and wait for response.
        
        Args:
            method: Method name
            params: Optional parameters
        
        Returns:
            Response object
        
        Raises:
            PipeConnectionError: If communication fails
            ProtocolError: If protocol error occurs
        """
        self.logger.debug(f"call() invoked for method: {method}, params: {params}")
        self._ensure_connected()
        
        with self._lock:
            try:
                # Create request
                request_id = self._get_next_request_id()
                self.logger.debug(f"Generated request ID: {request_id}")
                request = create_request(request_id, method, params)
                self.logger.debug(f"Created request object: id={request.id}, method={request.method}, params={request.params}")
                
                # Serialize and send
                self.logger.debug("Serializing request...")
                request_bytes = serialize_request(request)
                self.logger.debug(f"Serialized request: {len(request_bytes)} bytes")
                self.logger.debug(f"Sending request: {method} (ID: {request_id})")
                self._write_message(request_bytes)
                self.logger.debug("Request sent successfully, waiting for response...")
                
                # Read response
                self.logger.debug("Reading response from pipe...")
                response_bytes = self._read_message()
                self.logger.debug(f"Received response bytes: {len(response_bytes)} bytes")
                
                self.logger.debug("Deserializing response...")
                response = deserialize_response(response_bytes)
                self.logger.debug(f"Deserialized response: id={response.id}, has_error={response.error is not None}, has_result={response.result is not None}")
                
                # Verify response ID matches
                if response.id != request_id:
                    self.logger.warning(f"Response ID mismatch: expected {request_id}, got {response.id}")
                else:
                    self.logger.debug(f"Response ID matches request ID: {request_id}")
                
                if response.error:
                    self.logger.debug(f"Response contains error: code={response.error.code}, message={response.error.message}")
                else:
                    self.logger.debug(f"Response contains result: {type(response.result)}")
                
                # Send acknowledgment to server
                try:
                    self.logger.debug(f"Sending acknowledgment for request ID: {request_id}")
                    ack = Acknowledgment(id=request_id, status="received")
                    ack_bytes = self._serialize_acknowledgment(ack)
                    self._write_message(ack_bytes)
                    self.logger.debug(f"Acknowledgment sent for request ID: {request_id}")
                except Exception as e:
                    self.logger.warning(f"Failed to send acknowledgment: {e}")
                    # Don't fail the call if ack fails
                
                return response
                
            except (PipeConnectionError, ProtocolError) as e:
                # Mark as disconnected on error
                self.logger.error(f"Protocol/Connection error during call: {e}")
                self._connected = False
                raise
            except Exception as e:
                self.logger.error(f"Unexpected error during call: {e}", exc_info=True)
                self._connected = False
                raise PipeConnectionError(f"Unexpected error during call: {e}") from e
    
    def call_with_retry(self, method: str, params: Optional[Dict[str, Any]] = None, max_retries: int = 3) -> Response:
        """
        Call a method with automatic retry on connection errors.
        
        Args:
            method: Method name
            params: Optional parameters
            max_retries: Maximum number of retry attempts
        
        Returns:
            Response object
        
        Raises:
            PipeConnectionError: If all retries fail
        """
        self.logger.debug(f"call_with_retry: Starting call for method '{method}' with {max_retries} max retries")
        last_error = None
        
        for attempt in range(max_retries):
            try:
                self.logger.debug(f"call_with_retry: Attempt {attempt + 1}/{max_retries} for method '{method}'")
                return self.call(method, params)
            except PipeConnectionError as e:
                last_error = e
                self.logger.warning(f"call_with_retry: Attempt {attempt + 1}/{max_retries} failed: {e}")
                if attempt < max_retries - 1:
                    self.logger.debug(f"call_with_retry: Waiting {self.reconnect_delay}s before retry...")
                    time.sleep(self.reconnect_delay)
                    try:
                        self.logger.debug("call_with_retry: Disconnecting before retry...")
                        self.disconnect()
                    except Exception as disconnect_error:
                        self.logger.debug(f"call_with_retry: Error during disconnect (ignored): {disconnect_error}")
                    try:
                        self.logger.debug("call_with_retry: Attempting to reconnect...")
                        self.connect()
                        self.logger.debug("call_with_retry: Reconnected successfully")
                    except PipeConnectionError as connect_error:
                        self.logger.debug(f"call_with_retry: Reconnection failed (will retry): {connect_error}")
                else:
                    self.logger.error(f"call_with_retry: All {max_retries} attempts failed for method '{method}'")
        
        self.logger.error(f"call_with_retry: Giving up after {max_retries} failed attempts")
        raise last_error or PipeConnectionError("Call failed with unknown error")
    
    def __enter__(self):
        """Context manager entry."""
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit."""
        self.disconnect()

