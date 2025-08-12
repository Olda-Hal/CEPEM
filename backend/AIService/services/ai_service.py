import os
from typing import Dict, Any
from datetime import datetime
try:
    import openai
except ImportError:
    openai = None

# Import models directly
import sys
sys.path.append('..')
from models import (
    SymptomAnalysisRequest, SymptomAnalysisResponse, 
    MedicalQuestionRequest, MedicalQuestionResponse,
    DiagnosisAssistanceRequest, DiagnosisAssistanceResponse,
    Condition, UrgencyLevel, Source, DiagnosticSuggestion,
    RecommendedTest, TreatmentSuggestion
)

class AIServiceCore:
    def __init__(self):
        self.openai_api_key = os.getenv("OPENAI_API_KEY")
        self.model_name = os.getenv("AI_MODEL_NAME", "gpt-3.5-turbo")
        self.max_tokens = int(os.getenv("MAX_TOKENS", "1000"))
        self.temperature = float(os.getenv("TEMPERATURE", "0.3"))
        
        if self.openai_api_key:
            openai.api_key = self.openai_api_key

    async def analyze_symptoms_with_ai(self, request: SymptomAnalysisRequest) -> SymptomAnalysisResponse:
        """Analýza symptomů pomocí AI modelu"""
        try:
            # Vytvoření promptu pro AI
            prompt = self._create_symptom_analysis_prompt(request)
            
            # Volání AI modelu (mock implementace)
            ai_response = await self._call_ai_model(prompt)
            
            # Zpracování odpovědi
            return self._process_symptom_analysis_response(ai_response, request)
            
        except Exception as e:
            raise Exception(f"Chyba při AI analýze symptomů: {str(e)}")

    async def answer_medical_question_with_ai(self, request: MedicalQuestionRequest) -> MedicalQuestionResponse:
        """Odpovídání na lékařské otázky pomocí AI"""
        try:
            prompt = self._create_medical_question_prompt(request)
            ai_response = await self._call_ai_model(prompt)
            return self._process_medical_question_response(ai_response, request)
            
        except Exception as e:
            raise Exception(f"Chyba při AI odpovídání: {str(e)}")

    async def provide_diagnosis_assistance_with_ai(self, request: DiagnosisAssistanceRequest) -> DiagnosisAssistanceResponse:
        """Diagnostická asistence pomocí AI"""
        try:
            prompt = self._create_diagnosis_assistance_prompt(request)
            ai_response = await self._call_ai_model(prompt)
            return self._process_diagnosis_assistance_response(ai_response, request)
            
        except Exception as e:
            raise Exception(f"Chyba při AI diagnostické asistenci: {str(e)}")

    def _create_symptom_analysis_prompt(self, request: SymptomAnalysisRequest) -> str:
        """Vytvoření promptu pro analýzu symptomů"""
        symptoms_text = ", ".join([s.name for s in request.symptoms])
        
        prompt = f"""
        Jako AI asistent pro zdravotnictví, analyzuj následující symptomy a poskytni strukturovanou odpověď:

        Symptomy: {symptoms_text}
        
        Informace o pacientovi:
        """
        
        if request.patient_info:
            if request.patient_info.age:
                prompt += f"- Věk: {request.patient_info.age} let\n"
            if request.patient_info.gender:
                prompt += f"- Pohlaví: {request.patient_info.gender}\n"
            if request.patient_info.medical_history:
                prompt += f"- Anamnéza: {', '.join(request.patient_info.medical_history)}\n"
        
        prompt += """
        Požadovaný formát odpovědi (JSON):
        {
            "possible_conditions": [
                {
                    "name": "název diagnózy",
                    "probability": 0.7,
                    "description": "popis",
                    "severity": "low/medium/high/critical"
                }
            ],
            "recommendations": ["doporučení 1", "doporučení 2"],
            "urgency_level": "low/medium/high/critical",
            "confidence_score": 0.8,
            "should_seek_immediate_care": false
        }
        
        Odpověz v češtině a buď konzervativní s diagnózami.
        """
        
        return prompt

    def _create_medical_question_prompt(self, request: MedicalQuestionRequest) -> str:
        """Vytvoření promptu pro lékařské otázky"""
        prompt = f"""
        Jako AI asistent pro zdravotnictví, odpověz na následující lékařskou otázku:

        Otázka: {request.question}
        
        Kontext: {request.context or "Žádný specifický kontext"}
        Specializace: {request.specialization or "Obecná medicína"}
        
        Poskytni odpověď ve formátu JSON:
        {{
            "answer": "detailní odpověď",
            "sources": [
                {{
                    "title": "název zdroje",
                    "type": "guideline/study/textbook",
                    "reliability_score": 0.9
                }}
            ],
            "confidence_score": 0.8,
            "follow_up_questions": ["otázka 1", "otázka 2"],
            "related_topics": ["téma 1", "téma 2"]
        }}
        
        Odpověz v češtině a buď přesný s medicínskými informacemi.
        """
        
        return prompt

    def _create_diagnosis_assistance_prompt(self, request: DiagnosisAssistanceRequest) -> str:
        """Vytvoření promptu pro diagnostickou asistenci"""
        symptoms_text = ", ".join([s.name for s in request.symptoms])
        
        prompt = f"""
        Jako AI asistent pro diagnostiku, poskytni diagnostickou asistenci:

        Symptomy: {symptoms_text}
        
        Informace o pacientovi:
        """
        
        if request.patient_info:
            prompt += f"- Věk: {request.patient_info.age or 'neznámý'}\n"
            prompt += f"- Pohlaví: {request.patient_info.gender or 'neznámé'}\n"
            
        if request.test_results:
            prompt += f"Výsledky testů: {len(request.test_results)} výsledků\n"
            
        if request.clinical_notes:
            prompt += f"Klinické poznámky: {request.clinical_notes}\n"
        
        prompt += """
        Formát odpovědi (JSON):
        {
            "suggested_diagnoses": [
                {
                    "diagnosis": "název diagnózy",
                    "icd_code": "ICD-10 kód",
                    "probability": 0.7,
                    "supporting_evidence": ["důkaz 1"],
                    "contradicting_evidence": ["proti-důkaz 1"]
                }
            ],
            "recommended_tests": [
                {
                    "test_name": "název testu",
                    "priority": "urgent/routine/optional",
                    "reason": "důvod"
                }
            ],
            "treatment_suggestions": [
                {
                    "treatment": "léčba",
                    "type": "medication/procedure/lifestyle",
                    "priority": "vysoká/střední/nízká"
                }
            ],
            "confidence_score": 0.8
        }
        """
        
        return prompt

    async def _call_ai_model(self, prompt: str) -> Dict[str, Any]:
        """Volání AI modelu (mock implementace)"""
        # V produkci by zde bylo skutečné volání OpenAI API
        # Pro demonstraci vracíme mock odpověď
        
        if "symptom" in prompt.lower() or "symptom" in prompt.lower():
            return {
                "possible_conditions": [
                    {
                        "name": "Virová infekce",
                        "probability": 0.6,
                        "description": "Běžná virová infekce dýchacích cest",
                        "severity": "low"
                    }
                ],
                "recommendations": ["Odpočinek", "Zvýšený příjem tekutin"],
                "urgency_level": "low",
                "confidence_score": 0.7,
                "should_seek_immediate_care": False
            }
        elif "question" in prompt.lower():
            return {
                "answer": "Detailní odpověď na lékařskou otázku založená na aktuálních guidelines.",
                "sources": [
                    {
                        "title": "ESC Guidelines 2024",
                        "type": "guideline",
                        "reliability_score": 0.95
                    }
                ],
                "confidence_score": 0.85,
                "follow_up_questions": ["Jaké jsou rizikové faktory?"],
                "related_topics": ["Prevence", "Léčba"]
            }
        else:
            return {
                "suggested_diagnoses": [
                    {
                        "diagnosis": "Funkční porucha",
                        "icd_code": "K59.0",
                        "probability": 0.5,
                        "supporting_evidence": ["Anamnéza"],
                        "contradicting_evidence": []
                    }
                ],
                "recommended_tests": [
                    {
                        "test_name": "Základní biochemie",
                        "priority": "routine",
                        "reason": "Vyloučení organické příčiny"
                    }
                ],
                "treatment_suggestions": [
                    {
                        "treatment": "Symptomatická léčba",
                        "type": "medication",
                        "priority": "střední"
                    }
                ],
                "confidence_score": 0.6
            }

    def _process_symptom_analysis_response(self, ai_response: Dict[str, Any], request: SymptomAnalysisRequest) -> SymptomAnalysisResponse:
        """Zpracování odpovědi AI pro analýzu symptomů"""
        from uuid import uuid4
        
        analysis_id = f"analysis_{datetime.now().strftime('%Y%m%d_%H%M%S')}_{str(uuid4())[:8]}"
        
        conditions = []
        for cond in ai_response.get("possible_conditions", []):
            conditions.append(Condition(
                name=cond["name"],
                probability=cond["probability"],
                description=cond["description"],
                severity=UrgencyLevel(cond["severity"]),
                recommendations=ai_response.get("recommendations", [])
            ))
        
        return SymptomAnalysisResponse(
            response_id=analysis_id,
            timestamp=datetime.now(),
            success=True,
            analysis_id=analysis_id,
            possible_conditions=conditions,
            general_recommendations=ai_response.get("recommendations", []),
            urgency_level=UrgencyLevel(ai_response.get("urgency_level", "low")),
            confidence_score=ai_response.get("confidence_score", 0.5),
            disclaimer="Tato analýza slouží pouze jako pomocný nástroj a nenahrazuje lékařské vyšetření.",
            should_seek_immediate_care=ai_response.get("should_seek_immediate_care", False)
        )

    def _process_medical_question_response(self, ai_response: Dict[str, Any], request: MedicalQuestionRequest) -> MedicalQuestionResponse:
        """Zpracování odpovědi AI pro lékařské otázky"""
        from uuid import uuid4
        
        sources = []
        for source in ai_response.get("sources", []):
            sources.append(Source(
                title=source["title"],
                type=source["type"],
                reliability_score=source.get("reliability_score", 0.8)
            ))
        
        return MedicalQuestionResponse(
            response_id=str(uuid4()),
            timestamp=datetime.now(),
            success=True,
            answer=ai_response.get("answer", ""),
            sources=sources,
            confidence_score=ai_response.get("confidence_score", 0.7),
            follow_up_questions=ai_response.get("follow_up_questions", []),
            related_topics=ai_response.get("related_topics", [])
        )

    def _process_diagnosis_assistance_response(self, ai_response: Dict[str, Any], request: DiagnosisAssistanceRequest) -> DiagnosisAssistanceResponse:
        """Zpracování odpovědi AI pro diagnostickou asistenci"""
        from uuid import uuid4
        
        suggested_diagnoses = []
        for diag in ai_response.get("suggested_diagnoses", []):
            suggested_diagnoses.append(DiagnosticSuggestion(
                diagnosis=diag["diagnosis"],
                icd_code=diag.get("icd_code"),
                probability=diag["probability"],
                supporting_evidence=diag.get("supporting_evidence", []),
                contradicting_evidence=diag.get("contradicting_evidence", [])
            ))
        
        recommended_tests = []
        for test in ai_response.get("recommended_tests", []):
            recommended_tests.append(RecommendedTest(
                test_name=test["test_name"],
                priority=test["priority"],
                reason=test["reason"]
            ))
        
        treatment_suggestions = []
        for treatment in ai_response.get("treatment_suggestions", []):
            treatment_suggestions.append(TreatmentSuggestion(
                treatment=treatment["treatment"],
                type=treatment["type"],
                priority=treatment["priority"],
                contraindications=[],
                monitoring_required=False
            ))
        
        return DiagnosisAssistanceResponse(
            response_id=str(uuid4()),
            timestamp=datetime.now(),
            success=True,
            suggested_diagnoses=suggested_diagnoses,
            recommended_tests=recommended_tests,
            treatment_suggestions=treatment_suggestions,
            referral_recommendations=[],
            confidence_score=ai_response.get("confidence_score", 0.6)
        )
