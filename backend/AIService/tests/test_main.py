import pytest
from fastapi.testclient import TestClient
from main import app

client = TestClient(app)

def test_health_check():
    """Test health check endpoint"""
    response = client.get("/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"
    assert data["service"] == "AI Service"

def test_ai_info():
    """Test AI info endpoint"""
    response = client.get("/api/ai/info")
    assert response.status_code == 200
    data = response.json()
    assert data["service"] == "CEPEM AI Service"
    assert "capabilities" in data
    assert len(data["capabilities"]) > 0

def test_ai_stats():
    """Test AI stats endpoint"""
    response = client.get("/api/ai/stats")
    assert response.status_code == 200
    data = response.json()
    assert "total_analyses" in data
    assert "average_confidence" in data

# Mock auth token pro testování
mock_token = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test"

def test_symptom_analysis():
    """Test symptom analysis endpoint"""
    test_data = {
        "symptoms": ["bolest hlavy", "horečka"],
        "patient_age": 30,
        "patient_gender": "female"
    }
    
    response = client.post(
        "/api/ai/analyze-symptoms",
        json=test_data,
        headers={"Authorization": mock_token}
    )
    
    assert response.status_code == 200
    data = response.json()
    assert "analysis_id" in data
    assert "possible_conditions" in data
    assert "recommendations" in data
    assert "urgency_level" in data
    assert "confidence_score" in data

def test_medical_question():
    """Test medical question endpoint"""
    test_data = {
        "question": "Jaké jsou příznaky diabetu?",
        "specialization": "endokrinologie"
    }
    
    response = client.post(
        "/api/ai/medical-question",
        json=test_data,
        headers={"Authorization": mock_token}
    )
    
    assert response.status_code == 200
    data = response.json()
    assert "answer" in data
    assert "sources" in data
    assert "confidence_score" in data
    assert "follow_up_questions" in data

def test_diagnosis_assistance():
    """Test diagnosis assistance endpoint"""
    test_data = {
        "patient_data": {"age": 45, "gender": "male"},
        "symptoms": ["chest pain", "shortness of breath"],
        "medical_history": ["hypertension"]
    }
    
    response = client.post(
        "/api/ai/diagnosis-assistance",
        json=test_data,
        headers={"Authorization": mock_token}
    )
    
    assert response.status_code == 200
    data = response.json()
    assert "suggested_diagnoses" in data
    assert "recommended_tests" in data
    assert "treatment_suggestions" in data
    assert "confidence_score" in data

def test_drug_interactions():
    """Test drug interaction endpoint"""
    test_data = {
        "medications": ["warfarin", "aspirin"],
        "patient_age": 65
    }
    
    response = client.post(
        "/api/ai/drug-interactions",
        json=test_data,
        headers={"Authorization": mock_token}
    )
    
    assert response.status_code == 200
    data = response.json()
    assert "interactions" in data
    assert "safe_combinations" in data
    assert "warnings" in data

def test_unauthorized_access():
    """Test unauthorized access to protected endpoints"""
    test_data = {"symptoms": ["test"]}
    
    response = client.post("/api/ai/analyze-symptoms", json=test_data)
    assert response.status_code == 403  # No auth header

def test_invalid_symptom_data():
    """Test invalid data handling"""
    test_data = {
        "symptoms": [],  # Empty symptoms
        "patient_age": -5  # Invalid age
    }
    
    response = client.post(
        "/api/ai/analyze-symptoms",
        json=test_data,
        headers={"Authorization": mock_token}
    )
    
    # Should still return 200 but handle gracefully
    assert response.status_code == 200
