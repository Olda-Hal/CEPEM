#!/bin/bash

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🏗️  CEPEM Platform - Build & Test Script${NC}"
echo "=================================================="

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --tests-only      Run only tests (skip main build)"
    echo "  --no-tests        Build without running tests"
    echo "  --clean          Clean all Docker images and volumes before build"
    echo "  --help           Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                # Build platform and run tests"
    echo "  $0 --tests-only   # Run tests only"
    echo "  $0 --no-tests     # Build without tests"
    echo "  $0 --clean        # Clean build with tests"
}

# Parse command line arguments
TESTS_ONLY=false
NO_TESTS=false
CLEAN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --tests-only)
            TESTS_ONLY=true
            shift
            ;;
        --no-tests)
            NO_TESTS=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            show_usage
            exit 1
            ;;
    esac
done

# Clean function
clean_docker() {
    echo -e "${YELLOW}🧹 Cleaning Docker resources...${NC}"
    docker compose down --volumes --rmi all --remove-orphans 2>/dev/null || true
    docker compose -f docker-compose.test.yml down --volumes --rmi all --remove-orphans 2>/dev/null || true
    docker system prune -f
    echo -e "${GREEN}✅ Cleanup completed${NC}"
}

# Build main platform
build_platform() {
    echo -e "${BLUE}🏗️  Building CEPEM Platform...${NC}"
    docker compose build --no-cache
    echo -e "${GREEN}✅ Platform build completed${NC}"
}

# Run tests
run_tests() {
    echo -e "${BLUE}🧪 Running Tests...${NC}"

    mkdir -p ./test-results
    local status=0

    # Run one service at a time so a failing suite fails the whole run.
    for service in healthcare-api-tests database-api-tests frontend-tests; do
        echo "Running $service..."
        if ! docker compose -f docker-compose.test.yml run --build --rm "$service"; then
            echo -e "${RED}❌ $service failed${NC}"
            status=1
        fi
    done

    if [ $status -eq 0 ]; then
        docker compose -f docker-compose.test.yml run --rm --no-deps test-reporter || status=$?
    fi

    docker compose -f docker-compose.test.yml down --remove-orphans
    return $status
}

# Start platform
start_platform() {
    echo -e "${BLUE}🚀 Starting CEPEM Platform...${NC}"
    docker compose up -d
    
    echo ""
    echo -e "${GREEN}🎉 CEPEM Platform is running!${NC}"
    echo "=================================================="
    echo "📱 Frontend:          http://localhost:3000"
    echo "🏥 HealthcareAPI:     http://localhost:5000"
    echo "🗃️  DatabaseAPI:       http://localhost:5001"
    echo "🤖 AI Service:        interní (bez publikovaného portu)"
    echo "🗄️  MySQL Database:    localhost:3306"
    echo ""
    echo "To stop: docker compose down"
    echo "To view logs: docker compose logs -f"
    echo "=================================================="
}

# Main execution
main() {
    if [ "$CLEAN" = true ]; then
        clean_docker
    fi
    
    if [ "$TESTS_ONLY" = true ]; then
        echo -e "${YELLOW}🧪 Running tests only...${NC}"
        run_tests
    elif [ "$NO_TESTS" = true ]; then
        echo -e "${YELLOW}🏗️  Building without tests...${NC}"
        build_platform
        start_platform
    else
        echo -e "${YELLOW}🏗️  Building platform with tests...${NC}"
        build_platform
        
        echo ""
        echo -e "${BLUE}🧪 Running test suite...${NC}"
        if ! run_tests; then
            echo -e "${RED}❌ Tests failed; not starting the platform.${NC}"
            exit 1
        fi
        echo ""
        start_platform
    fi
}

# Trap to cleanup on exit
trap 'echo -e "\n${YELLOW}🛑 Build interrupted${NC}"' INT TERM

# Run main function
main
