from pydantic import BaseModel
from typing import List, Optional, Dict
from datetime import datetime
from enum import Enum

class UrgencyLevel(str, Enum):
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CRITICAL = "critical"

class Gender(str, Enum):
    MALE = "male"
    FEMALE = "female"
    OTHER = "other"

class AnalysisStatus(str, Enum):
    PENDING = "pending"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"

# Base models
class AIRequest(BaseModel):
    request_id: Optional[str] = None
    timestamp: Optional[datetime] = None
    user_id: Optional[str] = None

class AIResponse(BaseModel):
    response_id: str
    timestamp: datetime
    success: bool
    message: Optional[str] = None

# Symptom Analysis Models
class Symptom(BaseModel):
    name: str
    severity: Optional[int] = None  # 1-10 scale
    duration: Optional[str] = None  # e.g., "2 days", "1 week"
    description: Optional[str] = None

class PatientInfo(BaseModel):
    age: Optional[int] = None
    gender: Optional[Gender] = None
    weight: Optional[float] = None
    height: Optional[float] = None
    medical_history: Optional[List[str]] = None
    current_medications: Optional[List[str]] = None
    allergies: Optional[List[str]] = None

class Condition(BaseModel):
    name: str
    icd_code: Optional[str] = None
    probability: float
    description: str
    severity: UrgencyLevel
    recommendations: List[str]

class SymptomAnalysisRequest(AIRequest):
    symptoms: List[Symptom]
    patient_info: Optional[PatientInfo] = None

class SymptomAnalysisResponse(AIResponse):
    analysis_id: str
    possible_conditions: List[Condition]
    general_recommendations: List[str]
    urgency_level: UrgencyLevel
    confidence_score: float
    disclaimer: str
    should_seek_immediate_care: bool

# Medical Question Models
class MedicalQuestionRequest(AIRequest):
    question: str
    context: Optional[str] = None
    specialization: Optional[str] = None
    language: Optional[str] = "cs"

class Source(BaseModel):
    title: str
    url: Optional[str] = None
    type: str  # "guideline", "study", "textbook", etc.
    reliability_score: Optional[float] = None

class MedicalQuestionResponse(AIResponse):
    answer: str
    sources: List[Source]
    confidence_score: float
    follow_up_questions: List[str]
    related_topics: List[str]

# Diagnosis Assistance Models
class TestResult(BaseModel):
    test_name: str
    value: str
    unit: Optional[str] = None
    reference_range: Optional[str] = None
    abnormal: Optional[bool] = None

class DiagnosisAssistanceRequest(AIRequest):
    patient_info: PatientInfo
    symptoms: List[Symptom]
    test_results: Optional[List[TestResult]] = None
    clinical_notes: Optional[str] = None

class DiagnosticSuggestion(BaseModel):
    diagnosis: str
    icd_code: Optional[str] = None
    probability: float
    supporting_evidence: List[str]
    contradicting_evidence: List[str]

class RecommendedTest(BaseModel):
    test_name: str
    priority: str  # "urgent", "routine", "optional"
    reason: str
    expected_cost: Optional[float] = None

class TreatmentSuggestion(BaseModel):
    treatment: str
    type: str  # "medication", "procedure", "lifestyle", "referral"
    priority: str
    contraindications: List[str]
    monitoring_required: bool

class DiagnosisAssistanceResponse(AIResponse):
    suggested_diagnoses: List[DiagnosticSuggestion]
    recommended_tests: List[RecommendedTest]
    treatment_suggestions: List[TreatmentSuggestion]
    referral_recommendations: List[str]
    confidence_score: float
    additional_notes: Optional[str] = None

# Drug Information Models
class DrugInteractionRequest(AIRequest):
    medications: List[str]
    patient_age: Optional[int] = None
    patient_conditions: Optional[List[str]] = None

class DrugInteraction(BaseModel):
    drug1: str
    drug2: str
    severity: str  # "minor", "moderate", "major", "contraindicated"
    description: str
    recommendation: str

class DrugInteractionResponse(AIResponse):
    interactions: List[DrugInteraction]
    safe_combinations: List[str]
    general_warnings: List[str]

# Analysis History Models
class AnalysisHistory(BaseModel):
    analysis_id: str
    analysis_type: str
    timestamp: datetime
    user_id: str
    patient_id: Optional[str] = None
    status: AnalysisStatus
    results_summary: Optional[str] = None
