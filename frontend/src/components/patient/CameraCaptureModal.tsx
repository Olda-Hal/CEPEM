import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import BaseModal from '../BaseModal';

interface CameraCaptureModalProps {
  onClose: () => void;
  /** Receives the captured photo as a JPEG blob */
  onCapture: (blob: Blob) => void;
}

/** Camera capture modal: manages its own media stream lifecycle. */
export const CameraCaptureModal: React.FC<CameraCaptureModalProps> = ({ onClose, onCapture }) => {
  const { t } = useTranslation();
  const videoRef = useRef<HTMLVideoElement>(null);
  const [, setStream] = useState<MediaStream | null>(null);

  useEffect(() => {
    let activeStream: MediaStream | null = null;

    navigator.mediaDevices
      .getUserMedia({ video: { facingMode: 'user', width: 640, height: 480 } })
      .then((stream) => {
        activeStream = stream;
        setStream(stream);
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
        }
      })
      .catch((error) => {
        console.error('Error accessing camera:', error);
        onClose();
      });

    return () => {
      activeStream?.getTracks().forEach((track) => track.stop());
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const capture = () => {
    if (!videoRef.current) {
      return;
    }

    const canvas = document.createElement('canvas');
    canvas.width = videoRef.current.videoWidth;
    canvas.height = videoRef.current.videoHeight;
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      return;
    }

    ctx.drawImage(videoRef.current, 0, 0);
    canvas.toBlob((blob) => {
      if (blob) {
        onCapture(blob);
      }
    }, 'image/jpeg', 0.9);
  };

  return (
    <BaseModal title={t('patients.takePhoto')} onClose={onClose} contentClassName="camera-content">
      <video
        ref={videoRef}
        autoPlay
        playsInline
        className="camera-video"
      />
      <div className="camera-actions">
        <button onClick={onClose} className="btn-cancel">
          {t('common.cancel')}
        </button>
        <button onClick={capture} className="btn-capture">
          📷 {t('patients.capturePhoto')}
        </button>
      </div>
    </BaseModal>
  );
};

export default CameraCaptureModal;
