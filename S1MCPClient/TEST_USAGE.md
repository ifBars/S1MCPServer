# Comprehensive Test Script Usage

The `test_comprehensive.py` script provides a comprehensive testing suite for the S1MCPClient that allows you to connect to the MCP server and poll various game information.

## Quick Start

### Run All Tests
```bash
python test_comprehensive.py --mode all
```

This will run all available tests:
- Handshake and server information
- Player information and inventory
- NPC information
- Item information
- Property information
- Vehicle information
- Game state
- Heartbeat test

### Interactive Menu Mode
```bash
python test_comprehensive.py --mode interactive
```

This provides an interactive menu where you can select which tests to run.

### Continuous Polling Mode
```bash
python test_comprehensive.py --mode poll --interval 10
```

This continuously polls the server every N seconds (default: 5) for quick status updates.

## Command Line Options

- `--mode {all,poll,interactive}`: Choose the test mode
  - `all`: Run all tests once
  - `poll`: Continuous polling mode
  - `interactive`: Interactive menu
- `--interval INTERVAL`: Polling interval in seconds (for poll mode, default: 5)
- `--log-level {DEBUG,INFO,WARNING,ERROR}`: Set logging level (default: INFO)

## Examples

### Run all tests with debug logging
```bash
python test_comprehensive.py --mode all --log-level DEBUG
```

### Poll every 10 seconds
```bash
python test_comprehensive.py --mode poll --interval 10
```

### Interactive mode with warnings only
```bash
python test_comprehensive.py --mode interactive --log-level WARNING
```

## Features

- **Color-coded output**: Easy to read success/error messages
- **Comprehensive testing**: Tests all major game information endpoints
- **Error handling**: Gracefully handles connection errors and method failures
- **Flexible modes**: Choose between one-time tests, continuous polling, or interactive menu
- **Heartbeat testing**: Verifies the heartbeat mechanism is working

## Requirements

- Python 3.10+
- S1MCPClient dependencies installed (`pip install -r requirements.txt`)
- S1MCPServer mod running in the game
- TCP server listening on port 8765 (default)

## Troubleshooting

If you get connection errors:
1. Make sure the game is running with the S1MCPServer mod loaded
2. Verify the mod has started the TCP server (check game logs)
3. Check that the port matches your configuration (default: 8765)
4. Try increasing the connection timeout in `config.json`

For more detailed debugging, use `--log-level DEBUG` to see all communication details.

