export interface TestSummary {
  timestamp: string;
  services: Record<string, ServiceTestInfo>;
}

export interface ServiceTestInfo {
  status: 'completed' | 'failed' | 'running';
  files: number;
  duration?: number;
  testCount?: number;
  passedCount?: number;
  failedCount?: number;
  coverage?: CoverageInfo;
}

export interface CoverageInfo {
  lines: number;
  functions: number;
  branches: number;
  statements: number;
}

export interface TestFileInfo {
  name: string;
  path: string;
  size: number;
  modified: string;
}

export interface ServiceDetails {
  serviceName: string;
  files: TestFileInfo[];
  count: number;
}

export interface LiveStatus {
  timestamp: string;
  testResultsPath: boolean;
  services: Record<string, {
    hasResults: boolean;
    fileCount: number;
    lastUpdate: string | null;
  }>;
}
