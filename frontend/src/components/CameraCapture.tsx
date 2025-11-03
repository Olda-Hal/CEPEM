import React, { useRef, useState, useCallback, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import './CameraCapture.css';

interface CameraCaptureProps {
  onCapture: (imageData: Blob) => void;
  onCancel: () => void;
}

export const CameraCapture: React.FC<CameraCaptureProps> = ({ onCapture, onCancel }) => {
  const { t } = useTranslation();
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const viewportRef = useRef<HTMLDivElement>(null);
  const [stream, setStream] = useState<MediaStream | null>(null);
  const [capturedImage, setCapturedImage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cropFrameSize, setCropFrameSize] = useState({ width: 0, height: 0 });

  const updateCropFrame = useCallback(() => {
    if (videoRef.current && viewportRef.current) {
      const video = videoRef.current;
      const viewport = viewportRef.current;
      
      const videoRect = video.getBoundingClientRect();
      const minDimension = Math.min(videoRect.width, videoRect.height);
      
      setCropFrameSize({
        width: minDimension,
        height: minDimension
      });
    }
  }, []);

  const startCamera = useCallback(async () => {
    try {
      const mediaStream = await navigator.mediaDevices.getUserMedia({
        video: {
          width: { ideal: 1280 },
          height: { ideal: 720 },
          facingMode: 'user'
        }
      });
      
      setStream(mediaStream);
      if (videoRef.current) {
        videoRef.current.srcObject = mediaStream;
        videoRef.current.onloadedmetadata = () => {
          updateCropFrame();
        };
      }
    } catch (err) {
      console.error('Error accessing camera:', err);
      setError(t('patients.camera.accessDenied'));
    }
  }, [t, updateCropFrame]);

  useEffect(() => {
    startCamera();

    window.addEventListener('resize', updateCropFrame);

    return () => {
      if (stream) {
        stream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
      }
      window.removeEventListener('resize', updateCropFrame);
    };
  }, [startCamera, updateCropFrame]);

  const capturePhoto = () => {
    if (videoRef.current && canvasRef.current) {
      const video = videoRef.current;
      const canvas = canvasRef.current;
      const context = canvas.getContext('2d');

      const targetSize = 400;
      canvas.width = targetSize;
      canvas.height = targetSize;

      if (context) {
        const videoWidth = video.videoWidth;
        const videoHeight = video.videoHeight;
        
        const minDimension = Math.min(videoWidth, videoHeight);
        const sx = (videoWidth - minDimension) / 2;
        const sy = (videoHeight - minDimension) / 2;
        
        context.drawImage(
          video,
          sx, sy, minDimension, minDimension,
          0, 0, targetSize, targetSize
        );
        
        const imageDataUrl = canvas.toDataURL('image/jpeg', 0.85);
        setCapturedImage(imageDataUrl);
        
        if (stream) {
          stream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
        }
      }
    }
  };

  const retakePhoto = () => {
    setCapturedImage(null);
    startCamera();
  };

  const confirmCapture = () => {
    if (capturedImage) {
      fetch(capturedImage)
        .then(res => res.blob())
        .then(blob => {
          onCapture(blob);
          if (stream) {
            stream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
          }
        });
    }
  };

  const handleCancel = () => {
    if (stream) {
      stream.getTracks().forEach((track: MediaStreamTrack) => track.stop());
    }
    onCancel();
  };

  return (
    <div className="camera-capture-modal" onClick={(e) => e.stopPropagation()}>
      <div className="camera-capture-content" onClick={(e) => e.stopPropagation()}>
        <div className="camera-header">
          <h3>{t('patients.camera.title')}</h3>
          <button className="close-button" onClick={handleCancel}>×</button>
        </div>

        {error && (
          <div className="camera-error">
            {error}
          </div>
        )}

        <div className="camera-viewport" ref={viewportRef}>
          {!capturedImage ? (
            <>
              <video
                ref={videoRef}
                autoPlay
                playsInline
                className="camera-video"
              />
              {cropFrameSize.width > 0 && (
                <div 
                  className="crop-frame"
                  style={{
                    width: `${cropFrameSize.width}px`,
                    height: `${cropFrameSize.height}px`
                  }}
                />
              )}
              <canvas ref={canvasRef} style={{ display: 'none' }} />
            </>
          ) : (
            <img src={capturedImage} alt="Captured" className="captured-image" />
          )}
        </div>

        <div className="camera-controls">
          {!capturedImage ? (
            <>
              <button className="button secondary" onClick={handleCancel}>
                {t('common.cancel')}
              </button>
              <button className="button primary capture-button" onClick={capturePhoto}>
                📷 {t('patients.camera.capture')}
              </button>
            </>
          ) : (
            <>
              <button className="button secondary" onClick={retakePhoto}>
                {t('patients.camera.retake')}
              </button>
              <button className="button primary" onClick={confirmCapture}>
                {t('patients.camera.confirm')}
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
