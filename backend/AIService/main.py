from datetime import datetime

from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.concurrency import run_in_threadpool

from services.intake_parser import extract_intake_form, page_images_for_vision
from services.vision_extractor import extract_with_vision, vision_enabled

MAX_UPLOAD_BYTES = 20 * 1024 * 1024

app = FastAPI(
    title="CEPEM AI Service",
    description="Paper intake form digitisation (PDF → structured fields)",
    version="1.0.0",
)


@app.get("/health")
async def health_check():
    return {
        "status": "healthy",
        "service": "AI Service",
        "timestamp": datetime.utcnow().isoformat(),
        "version": "1.0.0",
        "visionModel": vision_enabled(),
    }


@app.get("/api/info")
async def get_info():
    return {
        "service": "CEPEM AI Service",
        "version": "1.0.0",
        "visionModel": vision_enabled(),
    }


@app.post("/api/intake/extract")
async def extract_intake(file: UploadFile = File(...)):
    if file.content_type not in {"application/pdf", "application/octet-stream"}:
        raise HTTPException(status_code=415, detail="Only PDF uploads are supported")

    payload = await file.read(MAX_UPLOAD_BYTES + 1)
    if len(payload) > MAX_UPLOAD_BYTES:
        raise HTTPException(status_code=413, detail="PDF is larger than 20 MB")
    if not payload.startswith(b"%PDF-"):
        raise HTTPException(status_code=415, detail="File is not a PDF")

    try:
        result = await run_in_threadpool(extract_intake_form, payload)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc
    except Exception as exc:  # malformed/encrypted PDF
        raise HTTPException(status_code=422, detail=f"Could not read the PDF: {exc}") from exc

    if not result.is_intake_form:
        return result.as_dict()

    warnings = list(result.warnings)
    vision_fields = await extract_with_vision(await run_in_threadpool(page_images_for_vision, payload))
    if vision_fields:
        # The multimodal model reads handwriting better than Tesseract; its
        # values win, while local OCR fills anything the model omitted.
        result.fields.update(vision_fields)
        warnings.append("vision_model_used")
    elif vision_enabled():
        warnings.append("vision_model_failed")

    result.warnings = sorted(set(warnings))
    return result.as_dict()
