import { apiClient } from './api';

/**
 * Download a patient document with its original filename. The backend serves
 * .tdb measurements as binary data (never as a PDF), so this also works for
 * measurement files.
 */
export async function downloadPatientDocument(
  patientId: number,
  documentId: number,
  fileName: string
): Promise<void> {
  const blob = await apiClient.getBlob(
    `/api/patients/${patientId}/documents/${documentId}?download=true`
  );
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
