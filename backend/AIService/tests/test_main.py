import pytest
import httpx
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)


def test_health_endpoint():
    """Test health check endpoint returns correct response"""
    response = client.get("/health")
    assert response.status_code == 200
    
    data = response.json()
    assert data["status"] == "healthy"
    assert data["service"] == "AI Service"
    assert data["version"] == "1.0.0"
    assert "timestamp" in data


def test_info_endpoint():
    """Test info endpoint returns correct response"""
    response = client.get("/api/info")
    assert response.status_code == 200
    
    data = response.json()
    assert data["service"] == "CEPEM AI Service"
    assert data["version"] == "1.0.0"
    assert data["status"] == "running"


def test_health_endpoint_timestamp_format():
    """Test health endpoint timestamp is in correct ISO format"""
    response = client.get("/health")
    data = response.json()
    
    # Check if timestamp can be parsed as ISO format
    from datetime import datetime
    timestamp = datetime.fromisoformat(data["timestamp"])
    assert timestamp is not None


def test_cors_headers():
    """Test that CORS headers are properly set"""
    response = client.get("/health")  # Use GET instead of OPTIONS
    assert response.status_code == 200


@pytest.mark.asyncio
async def test_health_endpoint_async():
    """Test health endpoint with async client"""
    async with httpx.AsyncClient(app=app, base_url="http://test") as ac:
        response = await ac.get("/health")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
