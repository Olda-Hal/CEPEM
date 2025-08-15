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
    
    # Create test results directory
    mkdir -p ./test-results
    
    # Run test containers - let all tests complete regardless of failures
    echo "Starting test containers..."
    docker compose -f docker-compose.test.yml up --build --remove-orphans
    
    # Store test results but continue regardless
    TEST_EXIT_CODE=$?
    if [ $TEST_EXIT_CODE -eq 0 ]; then
        echo -e "${GREEN}✅ All tests completed successfully${NC}"
    else
        echo -e "${YELLOW}⚠️  Some tests failed (exit code: $TEST_EXIT_CODE)${NC}"
        echo -e "${YELLOW}⚠️  Platform will start anyway for test result review${NC}"
    fi
    
    # Cleanup test containers
    docker compose -f docker-compose.test.yml down --remove-orphans
    
    # Generate test report
    echo -e "${BLUE}📊 Generating test report...${NC}"
    ./scripts/generate-test-report.sh || echo -e "${YELLOW}⚠️  Test report generation failed${NC}"
    
    return 0  # Always return success to continue
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
    echo "🤖 AI Service:        http://localhost:8000"
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
        run_tests  # Always continue regardless of test results
        echo ""
        start_platform
    fi
}

# Trap to cleanup on exit
trap 'echo -e "\n${YELLOW}🛑 Build interrupted${NC}"' INT TERM

# Run main function
main
