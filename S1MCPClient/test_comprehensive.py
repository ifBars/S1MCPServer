"""Comprehensive test script for S1MCPClient that polls various game information."""

import sys
import json
import time
from pathlib import Path
from typing import Dict, Any, Optional
from datetime import datetime

# Add src to path
sys.path.insert(0, str(Path(__file__).parent / "src"))

from src.tcp_client import TcpClient, TcpConnectionError
from src.utils.config import Config
from src.utils.logger import setup_logger, get_logger


class Colors:
    """ANSI color codes for terminal output."""
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'


def print_header(text: str):
    """Print a formatted header."""
    print(f"\n{Colors.HEADER}{Colors.BOLD}{'=' * 70}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{text.center(70)}{Colors.ENDC}")
    print(f"{Colors.HEADER}{Colors.BOLD}{'=' * 70}{Colors.ENDC}\n")


def print_section(text: str):
    """Print a formatted section header."""
    print(f"\n{Colors.OKCYAN}{Colors.BOLD}▶ {text}{Colors.ENDC}")
    print(f"{Colors.OKCYAN}{'-' * 70}{Colors.ENDC}")


def print_success(text: str):
    """Print success message."""
    print(f"{Colors.OKGREEN}✅ {text}{Colors.ENDC}")


def print_error(text: str):
    """Print error message."""
    print(f"{Colors.FAIL}❌ {text}{Colors.ENDC}")


def print_warning(text: str):
    """Print warning message."""
    print(f"{Colors.WARNING}⚠️  {text}{Colors.ENDC}")


def print_info(text: str):
    """Print info message."""
    print(f"{Colors.OKBLUE}ℹ️  {text}{Colors.ENDC}")


def format_json(data: Any, indent: int = 2) -> str:
    """Format data as JSON string."""
    return json.dumps(data, indent=indent, ensure_ascii=False)


def call_method(client: TcpClient, method: str, params: Optional[Dict[str, Any]] = None, 
                show_result: bool = True) -> Optional[Dict[str, Any]]:
    """
    Call a method on the server and handle the response.
    
    Args:
        client: TCP client instance
        method: Method name to call
        params: Optional parameters
        show_result: Whether to print the result
    
    Returns:
        Result dictionary or None if error
    """
    try:
        print_info(f"Calling {method}...")
        if params:
            print(f"  Parameters: {format_json(params)}")
        
        response = client.call_with_retry(method, params or {})
        
        if response.error:
            print_error(f"{method} failed: {response.error.message} (code: {response.error.code})")
            if response.error.data:
                print(f"  Error details: {format_json(response.error.data)}")
            return None
        
        if show_result:
            print_success(f"{method} succeeded")
            print(f"\n{format_json(response.result, indent=2)}")
        
        return response.result
        
    except TcpConnectionError as e:
        print_error(f"Connection error calling {method}: {e}")
        return None
    except Exception as e:
        print_error(f"Unexpected error calling {method}: {e}")
        return None


def test_handshake(client: TcpClient) -> bool:
    """Test handshake and display available methods."""
    print_section("Handshake Test")
    
    result = call_method(client, "handshake", show_result=False)
    if not result:
        return False
    
    print_success("Handshake successful!")
    print(f"\n{Colors.BOLD}Server Information:{Colors.ENDC}")
    print(f"  Server Name: {result.get('server_name', 'Unknown')}")
    print(f"  Version: {result.get('version', 'Unknown')}")
    print(f"  Total Methods: {result.get('total_methods', 0)}")
    
    # Display method categories
    if 'method_categories' in result:
        print(f"\n{Colors.BOLD}Available Methods by Category:{Colors.ENDC}")
        categories = result['method_categories']
        for category, methods in categories.items():
            if methods:
                print(f"  {Colors.OKCYAN}{category.upper()}:{Colors.ENDC} {len(methods)} methods")
                for method in methods[:5]:  # Show first 5
                    print(f"    - {method}")
                if len(methods) > 5:
                    print(f"    ... and {len(methods) - 5} more")
    
    # Display integrations
    if 'integrations' in result:
        print(f"\n{Colors.BOLD}Integrations:{Colors.ENDC}")
        integrations = result['integrations']
        for name, available in integrations.items():
            status = f"{Colors.OKGREEN}✓{Colors.ENDC}" if available else f"{Colors.FAIL}✗{Colors.ENDC}"
            print(f"  {status} {name}")
    
    return True


def test_player_info(client: TcpClient):
    """Test player information methods."""
    print_section("Player Information")
    
    # Get player info
    player_info = call_method(client, "get_player", show_result=True)
    
    if player_info:
        # Try to get inventory
        print_section("Player Inventory")
        inventory = call_method(client, "get_player_inventory", show_result=True)


def test_npc_info(client: TcpClient):
    """Test NPC information methods."""
    print_section("NPC Information")
    
    # List NPCs
    npcs = call_method(client, "list_npcs", show_result=False)
    
    if npcs:
        npc_list = npcs.get('npcs', [])
        count = npcs.get('count', 0)
        print_success(f"Found {count} NPCs")
        
        if npc_list:
            print(f"\n{Colors.BOLD}Sample NPCs (showing first 5):{Colors.ENDC}")
            for npc in npc_list[:5]:
                npc_id = npc.get('npc_id', 'Unknown')
                name = npc.get('name', 'Unknown')
                print(f"  - {npc_id}: {name}")
            
            # Try to get details for first NPC
            if npc_list:
                first_npc_id = npc_list[0].get('npc_id')
                if first_npc_id:
                    print_section(f"NPC Details: {first_npc_id}")
                    call_method(client, "get_npc", {"npc_id": first_npc_id}, show_result=True)


def test_item_info(client: TcpClient):
    """Test item information methods."""
    print_section("Item Information")
    
    # List items
    items = call_method(client, "list_items", show_result=False)
    
    if items:
        item_list = items.get('items', [])
        count = items.get('count', 0)
        print_success(f"Found {count} items")
        
        if item_list:
            print(f"\n{Colors.BOLD}Sample Items (showing first 10):{Colors.ENDC}")
            for item in item_list[:10]:
                item_id = item.get('item_id', 'Unknown')
                name = item.get('name', 'Unknown')
                print(f"  - {item_id}: {name}")
            
            # Try to get details for first item
            if item_list:
                first_item_id = item_list[0].get('item_id')
                if first_item_id:
                    print_section(f"Item Details: {first_item_id}")
                    call_method(client, "get_item", {"item_id": first_item_id}, show_result=True)


def test_property_info(client: TcpClient):
    """Test property information methods."""
    print_section("Property Information")
    
    # List properties
    properties = call_method(client, "list_properties", show_result=False)
    
    if properties:
        property_list = properties.get('properties', [])
        count = properties.get('count', 0)
        print_success(f"Found {count} properties")
        
        if property_list:
            print(f"\n{Colors.BOLD}Sample Properties (showing first 5):{Colors.ENDC}")
            for property in property_list[:5]:
                property_id = property.get('property_id', 'Unknown')
                name = property.get('name', 'Unknown')
                print(f"  - {property_id}: {name}")


def test_vehicle_info(client: TcpClient):
    """Test vehicle information methods."""
    print_section("Vehicle Information")
    
    # List vehicles
    vehicles = call_method(client, "list_vehicles", show_result=False)
    
    if vehicles:
        vehicle_list = vehicles.get('vehicles', [])
        count = vehicles.get('count', 0)
        print_success(f"Found {count} vehicles")
        
        if vehicle_list:
            print(f"\n{Colors.BOLD}Sample Vehicles (showing first 5):{Colors.ENDC}")
            for vehicle in vehicle_list[:5]:
                vehicle_id = vehicle.get('vehicle_id', 'Unknown')
                name = vehicle.get('name', 'Unknown')
                print(f"  - {vehicle_id}: {name}")


def test_game_state(client: TcpClient):
    """Test game state information."""
    print_section("Game State Information")
    
    game_state = call_method(client, "get_game_state", show_result=True)


def test_heartbeat(client: TcpClient):
    """Test heartbeat mechanism."""
    print_section("Heartbeat Test")
    
    print_info("Sending heartbeat request...")
    result = call_method(client, "heartbeat", show_result=False)
    
    if result:
        print_success("Heartbeat successful!")
        if 'timestamp' in result:
            timestamp = result.get('timestamp')
            dt = datetime.fromtimestamp(timestamp / 1000)
            print(f"  Server timestamp: {dt.strftime('%Y-%m-%d %H:%M:%S')}")
    else:
        print_warning("Heartbeat failed or returned error")


def run_all_tests(client: TcpClient):
    """Run all available tests."""
    print_header("COMPREHENSIVE MCP SERVER TEST SUITE")
    print(f"{Colors.OKBLUE}Started at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}{Colors.ENDC}\n")
    
    # Test handshake first
    if not test_handshake(client):
        print_error("Handshake failed! Cannot continue tests.")
        return
    
    # Run all information tests
    test_player_info(client)
    test_npc_info(client)
    test_item_info(client)
    test_property_info(client)
    test_vehicle_info(client)
    test_game_state(client)
    test_heartbeat(client)
    
    print_header("TEST SUITE COMPLETE")
    print(f"{Colors.OKGREEN}All tests completed at: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}{Colors.ENDC}\n")


def run_polling_mode(client: TcpClient, interval: int = 5):
    """Run in continuous polling mode."""
    print_header("CONTINUOUS POLLING MODE")
    print_info(f"Polling every {interval} seconds. Press Ctrl+C to stop.\n")
    
    try:
        iteration = 0
        while True:
            iteration += 1
            print_header(f"Polling Iteration #{iteration}")
            print(f"{Colors.OKBLUE}Time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}{Colors.ENDC}\n")
            
            # Quick status check
            print_section("Quick Status Check")
            
            # Player info
            player = call_method(client, "get_player", show_result=False)
            if player:
                pos = player.get('position', {})
                print(f"  Player Position: ({pos.get('x', 0):.2f}, {pos.get('y', 0):.2f}, {pos.get('z', 0):.2f})")
                print(f"  Health: {player.get('health', 'N/A')}")
            
            # Game state
            game_state = call_method(client, "get_game_state", show_result=False)
            if game_state:
                scene = game_state.get('scene', 'Unknown')
                print(f"  Current Scene: {scene}")
            
            # Heartbeat
            heartbeat = call_method(client, "heartbeat", show_result=False)
            if heartbeat:
                print_success("Connection alive")
            
            print(f"\n{Colors.OKBLUE}Waiting {interval} seconds before next poll...{Colors.ENDC}\n")
            time.sleep(interval)
            
    except KeyboardInterrupt:
        print(f"\n{Colors.WARNING}Polling stopped by user{Colors.ENDC}\n")


def interactive_menu(client: TcpClient):
    """Interactive menu for testing specific methods."""
    print_header("INTERACTIVE TEST MENU")
    
    menu_options = {
        '1': ('Player Information', lambda: test_player_info(client)),
        '2': ('NPC Information', lambda: test_npc_info(client)),
        '3': ('Item Information', lambda: test_item_info(client)),
        '4': ('Property Information', lambda: test_property_info(client)),
        '5': ('Vehicle Information', lambda: test_vehicle_info(client)),
        '6': ('Game State', lambda: test_game_state(client)),
        '7': ('Heartbeat Test', lambda: test_heartbeat(client)),
        '8': ('Run All Tests', lambda: run_all_tests(client)),
        '9': ('Start Polling Mode', lambda: run_polling_mode(client, 5)),
        '0': ('Exit', None)
    }
    
    while True:
        print(f"\n{Colors.BOLD}Available Options:{Colors.ENDC}")
        for key, (desc, _) in menu_options.items():
            print(f"  {key}. {desc}")
        
        choice = input(f"\n{Colors.OKCYAN}Select an option: {Colors.ENDC}").strip()
        
        if choice == '0':
            print_info("Exiting...")
            break
        elif choice in menu_options:
            name, func = menu_options[choice]
            if func:
                try:
                    func()
                except KeyboardInterrupt:
                    print(f"\n{Colors.WARNING}Operation cancelled{Colors.ENDC}\n")
                except Exception as e:
                    print_error(f"Error: {e}")
        else:
            print_warning("Invalid option. Please try again.")


def main():
    """Main entry point."""
    import argparse
    
    parser = argparse.ArgumentParser(description='Comprehensive test script for S1MCPClient')
    parser.add_argument('--mode', choices=['all', 'poll', 'interactive'], default='all',
                       help='Test mode: all (run all tests), poll (continuous polling), interactive (menu)')
    parser.add_argument('--interval', type=int, default=5,
                       help='Polling interval in seconds (for poll mode)')
    parser.add_argument('--log-level', default='INFO',
                       choices=['DEBUG', 'INFO', 'WARNING', 'ERROR'],
                       help='Logging level')
    
    args = parser.parse_args()
    
    # Setup logging
    config = Config.from_file()
    config.log_level = args.log_level
    setup_logger(config.log_level)
    logger = get_logger()
    
    # Create TCP client
    client = TcpClient(
        host=config.host,
        port=config.port,
        timeout=config.connection_timeout,
        reconnect_delay=config.reconnect_delay
    )
    
    try:
        logger.info(f"Connecting to server at {config.host}:{config.port}...")
        print_info(f"Connecting to server at {config.host}:{config.port}...")
        client.connect()
        logger.info("Connected successfully!")
        print_success("Connected successfully!")
        
        # Run based on mode
        if args.mode == 'all':
            run_all_tests(client)
        elif args.mode == 'poll':
            run_polling_mode(client, args.interval)
        elif args.mode == 'interactive':
            interactive_menu(client)
        
    except TcpConnectionError as e:
        logger.error(f"Connection error: {e}")
        print_error(f"Connection Error: {e}")
        print("\nMake sure:")
        print("1. The game is running with the S1MCPServer mod loaded")
        print("2. The mod has started the TCP server on port 8765")
        sys.exit(1)
    except KeyboardInterrupt:
        print(f"\n{Colors.WARNING}Interrupted by user{Colors.ENDC}")
    except Exception as e:
        logger.error(f"Unexpected error: {e}", exc_info=True)
        print_error(f"Unexpected Error: {e}")
        sys.exit(1)
    finally:
        if client.is_connected():
            logger.info("Disconnecting...")
            print_info("Disconnecting...")
            client.disconnect()
            print_success("Disconnected")


if __name__ == "__main__":
    main()

