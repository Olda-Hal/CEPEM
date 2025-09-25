import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { testResultsApi } from '../utils/testResultsApi';
import { TestSummary, ServiceDetails, LiveStatus } from '../types/testResults';
import { AppHeader } from '../components/AppHeader';
import './TestDashboard.css';

const TestDashboard: React.FC = () => {
  const { t } = useTranslation();
  const [testSummary, setTestSummary] = useState<TestSummary | null>(null);
  const [liveStatus, setLiveStatus] = useState<LiveStatus | null>(null);
  const [selectedService, setSelectedService] = useState<string | null>(null);
  const [serviceDetails, setServiceDetails] = useState<ServiceDetails | null>(null);
  const [selectedFile, setSelectedFile] = useState<string | null>(null);
  const [fileContent, setFileContent] = useState<string | null>(null);
  const [coverageContent, setCoverageContent] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);

  const refreshData = async () => {
    try {
      const [summary, status] = await Promise.all([
        testResultsApi.getTestSummary(),
        testResultsApi.getLiveStatus()
      ]);
      setTestSummary(summary);
      setLiveStatus(status);
    } catch (error) {
      console.error('Error fetching test data:', error);
    }
  };

  useEffect(() => {
    const loadData = async () => {
      setLoading(true);
      await refreshData();
      setLoading(false);
    };
    loadData();
  }, []);

  useEffect(() => {
    if (!autoRefresh) return;
    
    const interval = setInterval(refreshData, 5000);
    return () => clearInterval(interval);
  }, [autoRefresh]);

  const handleServiceClick = async (serviceName: string) => {
    if (selectedService === serviceName) {
      setSelectedService(null);
      setServiceDetails(null);
      setSelectedFile(null);
      setFileContent(null);
      setCoverageContent(null);
      return;
    }

    try {
      const details = await testResultsApi.getServiceDetails(serviceName);
      setSelectedService(serviceName);
      setServiceDetails(details);
      setSelectedFile(null);
      setFileContent(null);
      setCoverageContent(null);
    } catch (error) {
      console.error('Error fetching service details:', error);
    }
  };

  const handleFileClick = async (fileName: string) => {
    if (!selectedService) return;

    try {
      const content = await testResultsApi.getTestFile(selectedService, fileName);
      setSelectedFile(fileName);
      setFileContent(content);
    } catch (error) {
      console.error('Error fetching file content:', error);
    }
  };

  const handleCoverageClick = async () => {
    if (!selectedService) return;

    try {
      const content = await testResultsApi.getCoverageReport(selectedService);
      setCoverageContent(content);
    } catch (error) {
      console.error('Error fetching coverage report:', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'completed': return 'status-completed';
      case 'failed': return 'status-failed';
      case 'running': return 'status-running';
      default: return 'status-unknown';
    }
  };

  const formatDuration = (duration?: number) => {
    if (!duration) return 'N/A';
    return `${(duration / 1000).toFixed(2)}s`;
  };

  const formatPercentage = (value?: number) => {
    if (value === undefined || value === null) return 'N/A';
    return `${value.toFixed(1)}%`;
  };

  if (loading) {
    return (
      <div className="test-dashboard">
        <div className="loading-container">
          <div className="loading-spinner"></div>
          <p>{t('loading')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="test-dashboard">
      <AppHeader sectionTitle={t('testDashboard.title')}>
        <label className="auto-refresh-control">
          <input
            type="checkbox"
            checked={autoRefresh}
            onChange={(e) => setAutoRefresh(e.target.checked)}
          />
          {t('testDashboard.autoRefresh')}
        </label>
        <button onClick={refreshData} className="refresh-button">
          {t('testDashboard.refresh')}
        </button>
      </AppHeader>

      <div className="test-dashboard-content">{liveStatus && (
        <div className="live-status-section">
          <h2>{t('testDashboard.liveStatus')}</h2>
          <div className="status-info">
            <p>
              {t('testDashboard.lastUpdated')}: {new Date(liveStatus.timestamp).toLocaleString()}
            </p>
            <p>
              {t('testDashboard.testResultsPath')}: <span className="status-indicator">{liveStatus.testResultsPath ? '✅' : '❌'}</span>
            </p>
          </div>
        </div>
      )}

      {testSummary && (
        <div className="test-summary-section">
          <div className="test-summary-header">
            <h2>{t('testDashboard.testSummary')}</h2>
            <span className="summary-timestamp">
              {new Date(testSummary.timestamp).toLocaleString()}
            </span>
          </div>

          <div className="services-grid">
            {Object.entries(testSummary.services).map(([serviceName, service]) => (
              <div
                key={serviceName}
                className={`service-card ${selectedService === serviceName ? 'selected' : ''}`}
                onClick={() => handleServiceClick(serviceName)}
              >
                <div className="service-header">
                  <h3 className="service-name">{serviceName}</h3>
                  <div className={`status-dot ${getStatusColor(service.status)}`}></div>
                </div>

                <div className="service-stats">
                  <div className="stat-row">
                    <span className="stat-label">{t('testDashboard.status')}:</span>
                    <span className="stat-value">{service.status}</span>
                  </div>
                  <div className="stat-row">
                    <span className="stat-label">{t('testDashboard.files')}:</span>
                    <span className="stat-value">{service.files}</span>
                  </div>
                  {(service as any).tests && (
                    <div className="stat-row">
                      <span className="stat-label">{t('testDashboard.tests')}:</span>
                      <span className="stat-value">{(service as any).passed}/{(service as any).tests}</span>
                    </div>
                  )}
                  {(service as any).failed > 0 && (
                    <div className="stat-row">
                      <span className="stat-label">{t('testDashboard.failed')}:</span>
                      <span className="stat-value fail-count">{(service as any).failed}</span>
                    </div>
                  )}
                  {(service as any).coverage !== undefined && (
                    <div className="stat-row">
                      <span className="stat-label">{t('testDashboard.coverage')}:</span>
                      <span className="stat-value">{formatPercentage((service as any).coverage)}</span>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {selectedService && serviceDetails && (
        <div className="service-details-section">
          <div className="details-header">
            <h2 className="details-title">{selectedService} {t('testDashboard.details')}</h2>
          </div>

          <h3 className="files-list-title">{t('testDashboard.testFiles')} ({serviceDetails.count})</h3>
          <div className="files-list">
            {serviceDetails.files.map((file) => (
              <div
                key={file.name}
                className="file-item"
                onClick={() => handleFileClick(file.name)}
              >
                <span className="file-name">{file.name}</span>
                <div className="file-meta">
                  <span>{(file.size / 1024).toFixed(1)} KB</span>
                  <span>{new Date(file.modified).toLocaleString()}</span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {selectedFile && fileContent && (
        <div className="file-content-section">
          <h2 className="file-content-title">{selectedFile}</h2>
          <pre className="file-content-display">{fileContent}</pre>
        </div>
      )}
      </div>
    </div>
  );
};

export default TestDashboard;
