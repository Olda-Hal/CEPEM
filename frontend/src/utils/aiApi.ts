// AI API client pro frontend integrace
// Soubor: frontend/src/utils/aiApi.ts

import React from 'react';

// AI Request/Response typy
export interface SymptomAnalysisRequest {
  symptoms: string[];
  patient_age?: number;
  patient_gender?: string;
  medical_history?: string[];
}

export interface SymptomAnalysisResponse {
  analysis_id: string;
  possible_conditions: Array<{
    condition: string;
    probability: number;
    description: string;
  }>;
  recommendations: string[];
  urgency_level: 'low' | 'medium' | 'high' | 'critical';
  confidence_score: number;
  disclaimer: string;
}

export interface MedicalQuestionRequest {
  question: string;
  context?: string;
  specialization?: string;
}

export interface MedicalQuestionResponse {
  answer: string;
  sources: string[];
  confidence_score: number;
  follow_up_questions: string[];
}

export interface DiagnosisAssistanceRequest {
  patient_data: {
    age?: number;
    gender?: string;
    medical_history?: string[];
  };
  symptoms: string[];
  test_results?: Record<string, any>;
  medical_history?: string[];
}

export interface DiagnosisAssistanceResponse {
  suggested_diagnoses: Array<{
    diagnosis: string;
    probability: number;
    icd_code?: string;
  }>;
  recommended_tests: string[];
  treatment_suggestions: string[];
  referral_recommendations: string[];
  confidence_score: number;
}

export interface DrugInteractionRequest {
  medications: string[];
  patient_age?: number;
  patient_conditions?: string[];
}

export interface DrugInteractionResponse {
  interactions: Array<{
    drug1: string;
    drug2: string;
    severity: string;
    description: string;
    recommendation: string;
  }>;
  safe_combinations: string[];
  warnings: string[];
}

// Pomocná funkce pro API volání
const makeApiRequest = async <T>(
  endpoint: string,
  method: 'GET' | 'POST' = 'GET',
  data?: any
): Promise<T> => {
  const token = localStorage.getItem('token');
  const baseURL = process.env.REACT_APP_AI_API_URL || 'http://localhost:8000/api';
  
  const response = await fetch(`${baseURL}${endpoint}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token && { Authorization: `Bearer ${token}` })
    },
    ...(data && { body: JSON.stringify(data) })
  });

  if (!response.ok) {
    throw new Error(`AI API Error: ${response.status} ${response.statusText}`);
  }

  return response.json();
};

// AI API client
export const aiApi = {
  /**
   * Analýza symptomů pomocí AI
   */
  analyzeSymptoms: async (data: SymptomAnalysisRequest): Promise<SymptomAnalysisResponse> => {
    return makeApiRequest<SymptomAnalysisResponse>('/ai/analyze-symptoms', 'POST', data);
  },

  /**
   * Položení lékařské otázky AI
   */
  askMedicalQuestion: async (data: MedicalQuestionRequest): Promise<MedicalQuestionResponse> => {
    return makeApiRequest<MedicalQuestionResponse>('/ai/medical-question', 'POST', data);
  },

  /**
   * Diagnostická asistence
   */
  getDiagnosisAssistance: async (data: DiagnosisAssistanceRequest): Promise<DiagnosisAssistanceResponse> => {
    return makeApiRequest<DiagnosisAssistanceResponse>('/ai/diagnosis-assistance', 'POST', data);
  },

  /**
   * Kontrola lékových interakcí
   */
  checkDrugInteractions: async (data: DrugInteractionRequest): Promise<DrugInteractionResponse> => {
    return makeApiRequest<DrugInteractionResponse>('/ai/drug-interactions', 'POST', data);
  },

  /**
   * Získání informací o AI service
   */
  getInfo: async () => {
    return makeApiRequest('/ai/info', 'GET');
  },

  /**
   * Získání statistik AI service
   */
  getStats: async () => {
    return makeApiRequest('/ai/stats', 'GET');
  }
};

// Utility funkce pro práci s AI odpověďmi
export const aiUtils = {
  /**
   * Formatování urgency level pro zobrazení
   */
  formatUrgencyLevel: (level: string): { text: string; color: string } => {
    switch (level) {
      case 'critical':
        return { text: 'Kritické', color: '#d32f2f' };
      case 'high':
        return { text: 'Vysoké', color: '#f57c00' };
      case 'medium':
        return { text: 'Střední', color: '#fbc02d' };
      case 'low':
      default:
        return { text: 'Nízké', color: '#388e3c' };
    }
  },

  /**
   * Formatování confidence score
   */
  formatConfidence: (score: number): string => {
    return `${Math.round(score * 100)}%`;
  },

  /**
   * Kontrola, zda je potřeba urgentní péče
   */
  requiresUrgentCare: (urgencyLevel: string): boolean => {
    return urgencyLevel === 'critical' || urgencyLevel === 'high';
  },

  /**
   * Vytvoření barevného indikátoru podle pravděpodobnosti
   */
  getProbabilityColor: (probability: number): string => {
    if (probability >= 0.8) return '#d32f2f';
    if (probability >= 0.6) return '#f57c00';
    if (probability >= 0.4) return '#fbc02d';
    return '#388e3c';
  }
};

// Hook pro používání AI funkcí v komponentách
export const useAI = () => {
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const analyzeSymptoms = async (data: SymptomAnalysisRequest) => {
    setLoading(true);
    setError(null);
    try {
      const result = await aiApi.analyzeSymptoms(data);
      return result;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chyba při analýze symptomů');
      throw err;
    } finally {
      setLoading(false);
    }
  };

  const askQuestion = async (data: MedicalQuestionRequest) => {
    setLoading(true);
    setError(null);
    try {
      const result = await aiApi.askMedicalQuestion(data);
      return result;
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Chyba při zpracování otázky');
      throw err;
    } finally {
      setLoading(false);
    }
  };

  return {
    analyzeSymptoms,
    askQuestion,
    loading,
    error,
    clearError: () => setError(null)
  };
};

export default aiApi;
