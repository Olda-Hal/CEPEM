import { apiClient } from './api';
import { TestSummary, ServiceDetails, TestFileInfo, LiveStatus } from '../types/testResults';

export const testResultsApi = {
  getTestSummary: async (): Promise<TestSummary> => {
    const response = await apiClient.get<TestSummary>('/api/testresults/summary');
    return response;
  },

  getServiceDetails: async (serviceName: string): Promise<ServiceDetails> => {
    const response = await apiClient.get<ServiceDetails>(`/api/testresults/details/${serviceName}`);
    return response;
  },

  getTestFile: async (serviceName: string, fileName: string): Promise<string> => {
    const response = await apiClient.get<string>(`/api/testresults/file/${serviceName}/${fileName}`);
    return response;
  },

  getCoverageReport: async (serviceName: string): Promise<string> => {
    const response = await apiClient.get<string>(`/api/testresults/coverage/${serviceName}`);
    return response;
  },

  getLiveStatus: async (): Promise<LiveStatus> => {
    const response = await apiClient.get<LiveStatus>('/api/testresults/live-status');
    return response;
  }
};
